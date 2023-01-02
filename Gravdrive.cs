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
// how often to check for changes in blocks such as damage
// 60 ticks = second
int checkEveryTicks = 30;
// --------------- settings ---------------

// no touchie below this point unless you know what you're doing ------------------------------------------------

IMyCockpit cockpit;
List<IMyCockpit> cockpits = new List<IMyCockpit>();
List<IMyGravityGenerator> gens = new List<IMyGravityGenerator>();
List<IMyArtificialMassBlock> masses = new List<IMyArtificialMassBlock>();
IMyTextSurface screen;
Vector3I fieldMax, fieldMin;

public Program()
{
    Echo("Loading...");
    GetBlocks();
    SetFieldSize();
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    Echo("Loaded succesfully!");
}

Vector3D moveInd = new Vector3D();
Vector3D velVect = new Vector3D();
double averageRuntime = 0;
bool waitTillErrorFixed = false;
bool idle = false;
int tickNum = 0;

public void Main(string argument, UpdateType updateSource)
{
    averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs * 0.01);
    if (averageRuntime > maxMs * 0.9)
    {
        return;
    }

    tickNum++;
    if (tickNum == checkEveryTicks)
    {
        tickNum = 0;
        SetFieldSize();
        GetBlocks(true);
    }

    if (waitTillErrorFixed) return;

    screen.WriteText("");
    Echo("Gravdrive");
    Echo("Gravity generators: " + gens.Count.ToString());
    Echo("Artificial masses: " + masses.Count.ToString());
    Echo("Auto field size: " + autoFieldSize.ToString());
    Echo("Lowest speed: " + lowestSpeed.ToString());
    Echo("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
    Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");
    screen.WriteText("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
    screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms\n", true);

    if (Math.Round(cockpit.GetShipSpeed(), 2) == 0 && NoPilotInput())
    {
        screen.WriteText("Velocity: Stationary\nStatus: Standby", true);
        Echo("Velocity: Stationary\nStatus: Standby");
        PowerOnOff(false);
        return;
    }
    else if (!cockpit.DampenersOverride && NoPilotInput())
    {
        screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipVelocities().LinearVelocity.Length(), 2).ToString() + " m/s\nStatus: Drifting", true);
        Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting");
        PowerOnOff(false);
        return;
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

    // vector of momentum in each direction, set to 0 based on a few factors
    velVect.X = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).X * IsZero(cockpit.MoveIndicator.X) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
    velVect.Y = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Y * IsZero(cockpit.MoveIndicator.Y) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
    velVect.Z = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Z * IsZero(cockpit.MoveIndicator.Z) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
    moveInd = cockpit.MoveIndicator * 9.8f;

    foreach (IMyGravityGenerator gen in gens)
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
public void PowerOnOff(bool power)
{
    if (idle != power) return;
    if (power == false && (averageRuntime > maxMs * 0.7)) return;
    foreach (IMyArtificialMassBlock mass in masses)
    {
        mass.Enabled = power;
    }
    foreach (IMyGravityGenerator gen in gens)
    {
        gen.Enabled = power;
    }
    idle = !power;
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
        foreach (IMyArtificialMassBlock mass in masses)
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
        foreach (IMyGravityGenerator gen in gens)
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
    gens.Clear();
    masses.Clear();
    fieldMax = Vector3I.Zero;
    fieldMin = Vector3I.Zero;
}
public void GetBlocks(bool clear = false)
{
    if (clear) Clear();
    waitTillErrorFixed = false;

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
        Echo("No main cockpit was found.");
        waitTillErrorFixed = true;
    }
    else if (maincockpitcount > 1)
    {
        Echo("More than 1 main cockpit were found.");
        waitTillErrorFixed = true;
    }
    else
    {
        screen = cockpit.GetSurface(0);
        screen.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        screen.FontSize = 1.4f;
    }

    GridTerminalSystem.GetBlocksOfType(masses);
    GridTerminalSystem.GetBlocksOfType(gens);

    for (int i = 0; i < masses.Count; i++)
    {
        if (!masses[i].IsFunctional)
        {
            masses.RemoveAt(i);
        }
    }
    for (int i = 0; i < gens.Count; i++)
    {
        if (!gens[i].IsFunctional)
        {
            gens.RemoveAt(i);
        }
    }

    if (gens.Count == 0)
    {
        Echo("No working gravity generators were found.");
        waitTillErrorFixed = true;
    }
    if (masses.Count == 0)
    {
        Echo("No working artificial masses were found.");
        waitTillErrorFixed = true;
    }
}
