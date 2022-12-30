// main cockpit is used for orientation, make sure you have ONE.
// artificial masses should be near the center of mass, otherwise the drive will rotate the ship

// average ms per tick, 0.3 ms is allowed on most servers
double maxMs = 0.3;
// run every 1, 10 or 100 tick, 1 tick = 16.6 ms
// use 1 if the ship won't stop moving when dampeners are on, otherwise use 10
// lower value = higher ms usage per tick
int updateFrequency = 10;

// no touchie below this point unless you know what you're doing ------------------------------------------------

IMyCockpit cockpit;
List<IMyCockpit> cockpits = new List<IMyCockpit>();
List<IMyGravityGenerator>[] gens = new List<IMyGravityGenerator>[6]; // 6 lists, one for each orientation
List<IMyArtificialMassBlock> masses = new List<IMyArtificialMassBlock>();
List<IMyGravityGenerator> allGens = new List<IMyGravityGenerator>();
List<IMyArtificialMassBlock> allMasses = new List<IMyArtificialMassBlock>();
List<IMyGyro> gyros = new List<IMyGyro>();
IMyTextSurface screen;

public Program()
{
    // get necessary blocks from grid and check for cockpit
    Echo("Loading...");
    try
    {
        GridTerminalSystem.GetBlocksOfType(cockpits);
        int maincockpitcount = 0;
        foreach (IMyCockpit mainCockpit in cockpits)
        {
            if (mainCockpit.IsMainCockpit)
            {
                maincockpitcount++;
                Echo("Found main cockpit.");
                cockpit = mainCockpit;
            }
        }
        if (cockpit == null)
        {
            Echo("No main cockpit was found, please add one and recompile.");
            return;
        }
        if (maincockpitcount > 1)
        {
            Echo("More than 1 main cockpit was found, please make sure there is only 1 and recompile.");
            return;
        }
        screen = cockpit.GetSurface(0);
        screen.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        screen.FontSize = 1.5f;
    }
    catch
    {
        Echo("No cockpit was found, please add one and recompile.");
        return;
    }

    GridTerminalSystem.GetBlocksOfType(allMasses);
    GridTerminalSystem.GetBlocksOfType(allGens);
    GridTerminalSystem.GetBlocksOfType(gyros);

    if (allGens.Count == 0)
    {
        Echo("No gravity generators were found, please add some and recompile.");
        return;
    }
    if (allMasses.Count == 0)
    {
        Echo("No artificial masses were found, please add some and recompile.");
        return;
    }

    Echo("Gravity generators: " + allGens.Count.ToString() + "\nArtificial masses: " + allMasses.Count.ToString());

    // init lists for gens
    for (int x = 0; x < 6; x++)
    {
        gens[x] = new List<IMyGravityGenerator>();
    }

    // sort gens into lists by orientation
    for (int i = 0; i < allGens.Count; i++)
    {
        switch (cockpits[0].Orientation.TransformDirectionInverse(allGens[i].Orientation.Up).ToString())
        {
            case "Up":
                gens[0].Add(allGens[i]);
                break;
            case "Down":
                gens[1].Add(allGens[i]);
                break;
            case "Right":
                gens[2].Add(allGens[i]);
                break;
            case "Left":
                gens[3].Add(allGens[i]);
                break;
            case "Forward":
                gens[4].Add(allGens[i]);
                break;
            case "Backward":
                gens[5].Add(allGens[i]);
                break;
        }
    }
    Echo("Ready!");
    switch (updateFrequency)
    {
        case 1:
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            break;
        case 10:
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            break;
        case 100:
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            break;
        default:
            Echo("Incorrect or no updatefrequency set, select one and recompile.");
            return;
    }
}

Vector3D moveInd = new Vector3D();
Vector3D velVect = new Vector3D();
double averageRuntime = 0;

public void Main(string argument, UpdateType updateSource)
{
    Echo("Gravdrive");
    Echo("Gravity generators: " + allGens.Count.ToString());
    Echo("Artificial masses: " + allMasses.Count.ToString());
    Echo("UpdateFrequency: " + updateFrequency.ToString() + ", every " + (updateFrequency * 16.6).ToString() + " ms");
    Echo("Dampeners: " + cockpit.DampenersOverride.ToString());
    screen.WriteText("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%\n");
    Echo("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
    if (Math.Round(cockpit.GetShipSpeed(), 2) == 0 && NoPilotInput())
    {
        screen.WriteText("Velocity: Stationary\nStatus: Standby", true);
        Echo("Velocity: Stationary\nStatus: Standby");
        PowerOnOff(false);
    }
    else if (!cockpit.DampenersOverride && NoPilotInput())
    {
        screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting", true);
        Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting");
        PowerOnOff(false);
    }
    else
    {
        string status = "Active";
        if (NoPilotInput())
        {
            status = "Braking";
        }
        screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status, true);
        Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status);
        PowerOnOff(true);
    }

    // vector of momentum in each direction, set to 0 if user input is detected in said direction or if dampeners are off (could be checked later but easier to integrate here)
    velVect.X = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).X * IsZero(cockpit.MoveIndicator.X) * Convert.ToInt32(cockpit.DampenersOverride);
    velVect.Y = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Y * IsZero(cockpit.MoveIndicator.Y) * Convert.ToInt32(cockpit.DampenersOverride);
    velVect.Z = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Z * IsZero(cockpit.MoveIndicator.Z) * Convert.ToInt32(cockpit.DampenersOverride);

    moveInd = cockpit.MoveIndicator * 9.8f;

    // set power value for each gravity drive in each orientation
    for (int orientation = 0; orientation < 6; orientation++)
    {
        for (int gen = 0; gen < gens[orientation].Count; gen++)
        {
            switch (orientation)
            {
                case 0:
                    gens[orientation][gen].GravityAcceleration = -GravAccerelation(moveInd.Y, -velVect.Y);
                    break;
                case 1:
                    gens[orientation][gen].GravityAcceleration = GravAccerelation(moveInd.Y, -velVect.Y);
                    break;
                case 2:
                    gens[orientation][gen].GravityAcceleration = -GravAccerelation(moveInd.X, -velVect.X);
                    break;
                case 3:
                    gens[orientation][gen].GravityAcceleration = GravAccerelation(moveInd.X, -velVect.X);
                    break;
                case 4:
                    gens[orientation][gen].GravityAcceleration = GravAccerelation(moveInd.Z, -velVect.Z);
                    break;
                case 5:
                    gens[orientation][gen].GravityAcceleration = -GravAccerelation(moveInd.Z, -velVect.Z);
                    break;
            }
        }
    }

    // average runtime per tick, script runs every updateFrequency ticks, so divide by that
    averageRuntime = averageRuntime * 0.99 + (((Runtime.LastRunTimeMs / updateFrequency) * 0.01));
    if (averageRuntime > maxMs * 0.9)
    {
        Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " ms\nSome functions are slowed\nto prevent overheating.");
        screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " ms\nSome functions are slowed\nto prevent overheating.", true);
    }
    else
    {
        Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");
        screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms", true);
    }
}
public void PowerOnOff(bool power)
{
    if (averageRuntime < maxMs * 0.9)
    {
        foreach (IMyArtificialMassBlock mass in allMasses)
        {
            mass.Enabled = power;
        }
        foreach (IMyGravityGenerator gen in allGens)
        {
            gen.Enabled = power;
        }
    }
}
public bool NoPilotInput()
{
    if (cockpit.MoveIndicator.X == 0 && cockpit.MoveIndicator.Y == 0 && cockpit.MoveIndicator.Z == 0)
    {
        return true;
    }
    return false;
}
public float GravAccerelation(double LinCtrl, double velVec)
{
    return (float)(LinCtrl + velVec);
}
public int IsZero(float value)
{
    if (value == 0)
    {
        return 1;
    }
    return 0;
}
