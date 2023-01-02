// main cockpit is used for orientation, make sure you have ONE.
// artificial masses should be near the center of mass, otherwise the drive will rotate the ship

// --------------- settings ---------------
// maximun allowed average ms per tick, 0.3 ms is allowed on most servers
double maxMs = 0.3;
// dampening will be disabled below this speed
// set to 105 to disable dampening completely
int lowestSpeed = 0;
// automatically set the field size of the gravity generators
bool autoFieldSize = true;
// --------------- settings ---------------

// no touchie below this point unless you know what you're doing ------------------------------------------------

IMyCockpit cockpit;
List<IMyCockpit> cockpits = new List<IMyCockpit>();
List<IMyGravityGenerator>[] gens = new List<IMyGravityGenerator>[6]; // 6 lists, one for each orientation
List<IMyGravityGenerator> allGens = new List<IMyGravityGenerator>();
List<IMyArtificialMassBlock> allMasses = new List<IMyArtificialMassBlock>();
IMyTextSurface screen;
Vector3I fieldMax, fieldMin;

public Program()
{
    Echo("Loading...");
    // init lists for gens
    for (int orientation = 0; orientation < 6; orientation++)
    {
        gens[orientation] = new List<IMyGravityGenerator>();
    }
    GetBlocks();
    SetFieldSize();
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    Echo("Loaded succesfully.");
}

Vector3D moveInd = new Vector3D();
Vector3D velVect = new Vector3D();
double averageRuntime = 0;
int everyTenTicks = 0;
bool skipNextTick = false;
bool waitTillErrorFixed = false;

public void Main(string argument, UpdateType updateSource)
{
    if (skipNextTick)
    {
        skipNextTick = false;
        return;
    }

    // to conserve available runtime, only check some things every 10 ticks
    everyTenTicks++;
    if (everyTenTicks == 10)
    {
        everyTenTicks = 0;
        SetFieldSize();
        GetBlocks(true);
    }
    if (waitTillErrorFixed) return;
    screen.WriteText("");
    Echo("Gravdrive");
    Echo("Gravity generators: " + allGens.Count.ToString());
    Echo("Artificial masses: " + allMasses.Count.ToString());
    Echo("Auto field size: " + autoFieldSize.ToString());
    Echo("Lowest speed: " + lowestSpeed.ToString());
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
        screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipVelocities().LinearVelocity.Length(), 2).ToString() + " m/s\nStatus: Drifting", true);
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
    velVect.X = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).X * IsZero(cockpit.MoveIndicator.X) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
    velVect.Y = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Y * IsZero(cockpit.MoveIndicator.Y) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
    velVect.Z = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Z * IsZero(cockpit.MoveIndicator.Z) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();

    moveInd = cockpit.MoveIndicator * 9.8f;

    // set power value for each gravity drive in each orientation
    for (int orientation = 0; orientation < 6; orientation++)
    {
        foreach (IMyGravityGenerator gen in gens[orientation])
        {
            switch (cockpit.Orientation.TransformDirectionInverse(gen.Orientation.Up))
            {
                case Base6Directions.Direction.Up:
                    gen.GravityAcceleration = -GravAccerelation(moveInd.Y, -velVect.Y);
                    break;
                case Base6Directions.Direction.Down:
                    gen.GravityAcceleration = GravAccerelation(moveInd.Y, -velVect.Y);
                    break;
                case Base6Directions.Direction.Right:
                    gen.GravityAcceleration = -GravAccerelation(moveInd.X, -velVect.X);
                    break;
                case Base6Directions.Direction.Left:
                    gen.GravityAcceleration = GravAccerelation(moveInd.X, -velVect.X);
                    break;
                case Base6Directions.Direction.Backward:
                    gen.GravityAcceleration = -GravAccerelation(moveInd.Z, -velVect.Z);
                    break;
                case Base6Directions.Direction.Forward:
                    gen.GravityAcceleration = GravAccerelation(moveInd.Z, -velVect.Z);
                    break;
            }
        }
    }

    // calculate average runtime per tick
    averageRuntime = averageRuntime * 0.99 + ((Runtime.LastRunTimeMs * 0.01));
    if (averageRuntime > maxMs * 0.9)
    {
        skipNextTick = true;
    }
    else
    {
        Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");
        screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms", true);
    }
}
public void PowerOnOff(bool power)
{
    // this uses a ton of runtime, don't turn off if using more than 70% of allowed ms
    if (power == false && (averageRuntime > maxMs * 0.7)) return;

    foreach (IMyArtificialMassBlock mass in allMasses)
    {
        mass.Enabled = power;
    }
    foreach (IMyGravityGenerator gen in allGens)
    {
        gen.Enabled = power;
    }
}
public bool NoPilotInput()
{
    return cockpit.MoveIndicator.X == 0 && cockpit.MoveIndicator.Y == 0 && cockpit.MoveIndicator.Z == 0;
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
public int UnderLowestSpeed()
{
    if (cockpit.GetShipVelocities().LinearVelocity.Length() < lowestSpeed)
    {
        return 0;
    }
    return 1;
}
public void SetFieldSize()
{
    if (fieldMax == Vector3I.Zero || fieldMin == Vector3I.Zero)
    {
        foreach (IMyArtificialMassBlock mass in allMasses)
        {
            if (fieldMax.X > mass.Max.X) fieldMax.X = mass.Max.X;
            if (fieldMax.Y > mass.Max.Y) fieldMax.Y = mass.Max.Y;
            if (fieldMax.Z > mass.Max.Z) fieldMax.Z = mass.Max.Z;

            if (fieldMin.X < mass.Min.X) fieldMin.X = mass.Min.X;
            if (fieldMin.Y < mass.Min.Y) fieldMin.Y = mass.Min.Y;
            if (fieldMin.Z < mass.Min.Z) fieldMin.Z = mass.Min.Z;
        }
    }
    if (autoFieldSize)
    {
        foreach (IMyGravityGenerator gen in allGens)
        {
            var height = GetLengthToCorner(gen.Orientation.Up, fieldMax, fieldMin, gen.Position);
            var width = GetLengthToCorner(gen.Orientation.Left, fieldMax, fieldMin, gen.Position);
            var depth = GetLengthToCorner(gen.Orientation.Forward, fieldMax, fieldMin, gen.Position);
            gen.FieldSize = new Vector3(width * 5 + 3, height * 5 + 3, depth * 5 + 3);
        }
    }
}
public float GetLengthToCorner(Base6Directions.Direction orientation, Vector3I max, Vector3I min, Vector3I pos)
{
    switch (orientation)
    {
        case Base6Directions.Direction.Backward:
        case Base6Directions.Direction.Forward:
            return Math.Max(Math.Abs(max.Z - pos.Z), Math.Abs(min.Z - pos.Z));
        case Base6Directions.Direction.Down:
        case Base6Directions.Direction.Up:
            return Math.Max(Math.Abs(max.Y - pos.Y), Math.Abs(min.Y - pos.Y));
        case Base6Directions.Direction.Right:
        case Base6Directions.Direction.Left:
            return Math.Max(Math.Abs(max.X - pos.X), Math.Abs(min.X - pos.X));
        default: throw new NullReferenceException("script brokie");
    }
}
public void Clear()
{
    cockpit = null;
    cockpits.Clear();
    screen = null;
    allGens.Clear();
    allMasses.Clear();
    fieldMax = Vector3I.Zero;
    fieldMin = Vector3I.Zero;
    for (int orientation = 0; orientation < 6; orientation++)
    {
        gens[orientation].Clear();
    }
}
// check for blocks, important if user is stupid and forgets to recompile
public void GetBlocks(bool clear = false)
{
    if (clear) Clear();

    waitTillErrorFixed = false;
    try
    {
        GridTerminalSystem.GetBlocksOfType(cockpits);
        int maincockpitcount = 0;
        foreach (IMyCockpit mainCockpit in cockpits)
        {
            if (mainCockpit.IsMainCockpit)
            {
                maincockpitcount++;
                cockpit = mainCockpit;
            }
        }
        if (cockpit == null)
        {
            Echo("No main cockpit was found, please add one.");
            waitTillErrorFixed = true;
            return;
        }
        if (maincockpitcount > 1)
        {
            Echo("More than 1 main cockpit was found, please make sure there is only 1.");
            waitTillErrorFixed = true;
            return;
        }
        screen = cockpit.GetSurface(0);
        screen.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        screen.FontSize = 1.2f;
    }
    catch
    {
        Echo("No cockpit was found, please add one.");
        waitTillErrorFixed = true;
        return;
    }

    GridTerminalSystem.GetBlocksOfType(allMasses);
    GridTerminalSystem.GetBlocksOfType(allGens);

    // check if blocks are damaged and remove from list
    for (int i = 0; i < allMasses.Count; i++)
    {
        if (!allMasses[i].IsFunctional)
        {
            allMasses.RemoveAt(i);
        }
    }
    for (int i = 0; i < allGens.Count; i++)
    {
        if (!allGens[i].IsFunctional)
        {
            allGens.RemoveAt(i);
        }
    }

    if (allGens.Count == 0)
    {
        Echo("No working gravity generators were found.");
        waitTillErrorFixed = true;
        return;
    }
    if (allMasses.Count == 0)
    {
        Echo("No working artificial masses were found.");
        waitTillErrorFixed = true;
        return;
    }

    // sort gens into lists by orientation
    for (int i = 0; i < allGens.Count; i++)
    {
        switch (cockpits[0].Orientation.TransformDirectionInverse(allGens[i].Orientation.Up))
        {
            case Base6Directions.Direction.Up:
                gens[0].Add(allGens[i]);
                break;
            case Base6Directions.Direction.Down:
                gens[1].Add(allGens[i]);
                break;
            case Base6Directions.Direction.Right:
                gens[2].Add(allGens[i]);
                break;
            case Base6Directions.Direction.Left:
                gens[3].Add(allGens[i]);
                break;
            case Base6Directions.Direction.Backward:
                gens[4].Add(allGens[i]);
                break;
            case Base6Directions.Direction.Forward:
                gens[5].Add(allGens[i]);
                break;
        }
    }
}
