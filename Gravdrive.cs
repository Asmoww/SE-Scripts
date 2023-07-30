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
// if you want to use spherical gens, place an EQUAL AMOUNT BEHIND AND IN FRONT of the gravity drives
// this way they will cancel out eachother, while still working as a shield, though they can still cause slight unwanted movement
// 0 if not using, 1 if using
int useSpherical = 0;
// shield is toggled between modes with the argument "shield" or directly with "push", "pull" or "off"
// --------------- settings ---------------

// no touchie below this point unless you know what you're doing ------------------------------------------------

IMyCockpit cockpit;
List<IMyCockpit> cockpits = new List<IMyCockpit>();
List<IMyGravityGenerator> gens = new List<IMyGravityGenerator>();
List<IMyGravityGeneratorSphere> spheres = new List<IMyGravityGeneratorSphere>();
List<IMyArtificialMassBlock> masses = new List<IMyArtificialMassBlock>();
IMyTextSurface screen;
Vector3I fieldMax, fieldMin;
Vector3D massCenterVector = new Vector3D();
int shield = 0;

public Program()
{
    Echo("Loading...");
    GetBlocks(false);
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
string shieldString = "Off";

public void Main(string arg, UpdateType updateSource)
{
    if (arg == "shield" || arg == "push" || arg == "pull" || arg == "off")
    {
        if (arg == "shield") arg = shield.ToString();
        switch (arg)
        {
            case "-1":
            case "off":
                shield = 0;
                shieldString = "Off";
                PowerOnOff(false, true);
                break;
            case "0":
            case "pull":
                shield = 1;
                shieldString = "Pull";
                PowerOnOff(true, true);
                break;
            case "1":
            case "push":
                shield = -1;
                shieldString = "Push";
                PowerOnOff(true, true);
                break;
        }
    }

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
    Echo("Shield:\n" + shieldString);
    Echo("Gravity generators: " + gens.Count.ToString() + " + " + spheres.Count.ToString() + "s");
    Echo("Artificial masses: " + masses.Count.ToString());
    Echo("Auto field size: " + autoFieldSize.ToString());
    Echo("Lowest speed: " + lowestSpeed.ToString());
    Echo("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
    Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");
    screen.WriteText("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
    screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms\n", true);
    screen.WriteText("Shield: " + shieldString + "\n", true);

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
                gen.GravityAcceleration = (float)-(moveInd.Y - velVect.Y);
                break;
            case Base6Directions.Direction.Down:
                gen.GravityAcceleration = (float)(moveInd.Y - velVect.Y);
                break;
            case Base6Directions.Direction.Right:
                gen.GravityAcceleration = (float)-(moveInd.X - velVect.X);
                break;
            case Base6Directions.Direction.Left:
                gen.GravityAcceleration = (float)(moveInd.X - velVect.X);
                break;
            case Base6Directions.Direction.Backward:
                gen.GravityAcceleration = (float)-(moveInd.Z - velVect.Z);
                break;
            case Base6Directions.Direction.Forward:
                gen.GravityAcceleration = (float)(moveInd.Z - velVect.Z);
                break;
        }
    }

    foreach (IMyGravityGeneratorSphere sphere in spheres)
    {
        if (shield != 0)
        {
            sphere.GravityAcceleration = shield * 9.8f;
        }
        else
        {
            sphere.GravityAcceleration = (float)-((moveInd.Z - velVect.Z) * useSpherical * IsFrontOrBack(sphere));
        }
    }
}
public void PowerOnOff(bool power, bool shieldToggle = false)
{
    if (shieldToggle)
    {
        foreach (IMyGravityGeneratorSphere sphere in spheres)
        {
            if (power)
            {
                sphere.Radius = 400;
                sphere.GravityAcceleration = shield * 9.8f;
            }
            else
            {
                SetFieldSize();
                sphere.GravityAcceleration = shield * 0f;
            }
            sphere.Enabled = power;
        }
    }
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
    if (shield == 0)
    {
        foreach (IMyGravityGeneratorSphere sphere in spheres)
        {
            sphere.Enabled = power;
        }
    }
    idle = !power;
}
public bool NoPilotInput()
{
    return cockpit.MoveIndicator.X == 0 && cockpit.MoveIndicator.Y == 0 && cockpit.MoveIndicator.Z == 0;
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
    if (autoFieldSize && shield == 0)
    {
        foreach (IMyGravityGenerator gen in gens)
        {
            var height = GetLengthToCorner(gen.Orientation.Up, fieldMax, fieldMin, gen.Position);
            var width = GetLengthToCorner(gen.Orientation.Left, fieldMax, fieldMin, gen.Position);
            var depth = GetLengthToCorner(gen.Orientation.Forward, fieldMax, fieldMin, gen.Position);
            gen.FieldSize = new Vector3(width * 5 + 3, height * 5 + 3, depth * 5 + 3);
        }
        foreach (IMyGravityGeneratorSphere sphere in spheres)
        {
            int highestLenght = (int)Math.Max(GetLengthToCorner(sphere.Orientation.Forward, fieldMax, fieldMin, sphere.Position),
            Math.Max(GetLengthToCorner(sphere.Orientation.Left, fieldMax, fieldMin, sphere.Position),
            GetLengthToCorner(sphere.Orientation.Up, fieldMax, fieldMin, sphere.Position)));
            sphere.Radius = (float)(highestLenght * 3.5);
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
        default: throw new NullReferenceException("script brokie (tylers fault)");
    }
}
public void Clear()
{
    cockpit = null;
    cockpits.Clear();
    screen = null;
    gens.Clear();
    masses.Clear();
    spheres.Clear();
    fieldMax = Vector3I.Zero;
    fieldMin = Vector3I.Zero;
    massCenterVector = Vector3D.Zero;
}
public void GetBlocks(bool clear)
{
    if (clear) Clear();
    waitTillErrorFixed = false;

    GridTerminalSystem.GetBlocksOfType(cockpits);
    int maincockpitcount = 0;
    try
    {
        foreach (IMyCockpit mainCockpit in cockpits)
        {
            if (mainCockpit.IsMainCockpit)
            {
                maincockpitcount++;
                cockpit = mainCockpit;
            }
        }
        if (maincockpitcount == 0)
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

        if (useSpherical != 0) GridTerminalSystem.GetBlocksOfType(spheres);
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
        for (int i = 0; i < spheres.Count; i++)
        {
            if (!spheres[i].IsFunctional)
            {
                spheres.RemoveAt(i);
            }
        }

        if (gens.Count == 0 && sphere.Count == 0)
        {
            Echo("No working gravity generators were found.");
            waitTillErrorFixed = true;
        }
        if (masses.Count == 0)
        {
            Echo("No working artificial masses were found.");
            waitTillErrorFixed = true;
        }
        foreach (IMyArtificialMassBlock mass in masses)
        {
            massCenterVector += -Vector3D.TransformNormal(mass.GetPosition() - cockpit.CenterOfMass, MatrixD.Transpose(cockpit.WorldMatrix));
        }
        massCenterVector /= masses.Count;
    }
    catch
    {
        Echo("Fix the above errors to continue.");
        waitTillErrorFixed = true;
    }
}
public int IsFrontOrBack(IMyGravityGeneratorSphere sphere)
{
    if ((-Vector3D.TransformNormal(sphere.GetPosition() - cockpit.CenterOfMass, MatrixD.Transpose(cockpit.WorldMatrix))).Z > massCenterVector.Z)
    {
        return 1;
    }
    else
    {
        return -1;
    }
}
