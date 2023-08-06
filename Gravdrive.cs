// set one cockpit as main, it's used for orientation

        // artificial masses should be near the center of mass, otherwise the drive will rotate the ship

        // spherical gens should only be place in front of or behind the artificial masses
        // - make sure to have an equal amount on both the front and back
        // - otherwise gravity drive will be disabled while shield is active to prevent unwanted movement

        // shield is toggled between modes with the argument "shield" or directly with "push", "pull" or "off"
        // gravity drive can be disabled with "drive", shield can still be used

        // name an LCD "Gdrive LCD" for use with hudlcd plugin

        // --------------- settings ---------------

        // maximun allowed average ms per tick, 0.3 ms is allowed on most servers
        double maxMs = 0.3;

        // dampening will be disabled below this speed
        // - set to above the max speed to disable completely 
        int lowestSpeed = 0;

        // automatically set the field size of the gravity generators
        bool autoFieldSize = true;

        // how often to check for changes in blocks such as damage
        // - 60 ticks = second
        int checkEveryTicks = 30;

        // keep drive on while shield is on
        // - will be disabled automatically if spherical gens aren't balanced correctly
        bool keepDriveOn = true;

        // which info to display on hudlcd 
        bool gdrive = true;
        bool gshield = true;
        bool blocks = true;
        bool runtime = true;
        bool efficiency = true; // efficiency loss from natural gravity

        // no touchie below this point unless you know what you're doing ------------------------------------------------

        IMyCockpit cockpit;
        List<IMyCockpit> cockpits = new List<IMyCockpit>();
        List<IMyGravityGenerator> gens = new List<IMyGravityGenerator>();
        List<IMyGravityGeneratorSphere> spheres = new List<IMyGravityGeneratorSphere>();
        List<IMyArtificialMassBlock> masses = new List<IMyArtificialMassBlock>();
        IMyTextSurface lcd;
        Vector3I fieldMax, fieldMin;
        Vector3D massCenterVector = new Vector3D();
        int shield = 0;
        int drive = 1;
        static List<IMyTextPanel> allLCDs = new List<IMyTextPanel>();
        bool nocontinue = false;

        public Program()
        {
            Echo("Loading...");
            GetBlocks(false);
            SetFieldSize();
            // Update10 works decently, Update100 doesn't work 
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            Echo("Loaded succesfully!");
        }

        Vector3D moveInd = new Vector3D();
        Vector3D velVect = new Vector3D();
        double averageRuntime = 0;
        bool waitTillErrorFixed = false;
        bool idle = false;
        int tickNum = 0;
        string shieldString = "<color=139,0,0,255>Off";
        string driveString = "...";

        public void Main(string arg, UpdateType updateSource)
        {
            if (arg == "shield" || arg == "push" || arg == "pull" || arg == "off" || arg == "drive")
            {
                if (arg == "shield") arg = shield.ToString();
                if (arg == "drive") arg = "d"+drive.ToString();
                switch (arg)
                {
                    case "-1":
                    case "off":
                        shield = 0;
                        shieldString = "<color=139,0,0,255>Off";
                        PowerOnOff(false, true);
                        break;
                    case "0":
                    case "pull":
                        shield = 1;
                        shieldString = "<color=0,200,200,255>Pull";
                        PowerOnOff(true, true);
                        break;
                    case "1":
                    case "push":
                        shield = -1;
                        shieldString = "<color=50,205,50,255>Push";
                        PowerOnOff(true, true);
                        break;
                    case "d0":
                        drive = 1;
                        break;
                    case "d1":
                        drive = 0;
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

            if ((spheres.Count > 0 && shield != 0 && !SpheresEqual()) || (gens.Count == 0 && shield != 0))
            {
                driveString = "Shld";
            }
            else if(drive == 0)
            {
                driveString = "Off";
            }
            else
            {
                driveString = "On";
            }
            TryWrite("");
            Echo("D: " + driveString);
            Echo("S: " + shieldString.Split('>')[1]);
            Echo("GDrive");
            Echo("Gravity generators: " + gens.Count.ToString() + " + " + spheres.Count.ToString() + "s");
            Echo("Artificial masses: " + masses.Count.ToString());
            Echo("Auto field size: " + autoFieldSize.ToString());
            Echo("Lowest speed: " + lowestSpeed.ToString());
            Echo("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
            Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");

            nocontinue = false;

            if ((spheres.Count > 0 && shield != 0 && !SpheresEqual()) || (gens.Count == 0 && shield != 0) || drive == 0 || (shield != 0 && !keepDriveOn) || masses.Count == 0)
            {
                if(spheres.Count == 0 || shield == 0 || drive == 0 || masses.Count == 0)
                {
                    if(gdrive) TryWrite("<color=211,211,211,255>Drive: <color=139,0,0,255>Off", true);                  
                    Echo("Velocity: ---\nStatus: Off");
                }
                else
                {
                    if(gdrive) TryWrite("<color=211,211,211,255>Drive: <color=0,139,139,255>Shield", true);
                    Echo("Velocity: ---\nStatus: Shield");
                }
                PowerOnOff(false);
                nocontinue = true;
            }
            else if (Math.Round(cockpit.GetShipSpeed(), 2) == 0 && NoPilotInput())
            {
                if (gdrive) TryWrite("<color=211,211,211,255>Drive: <color=173,216,230,255>Standby", true);
                Echo("Velocity: ---\nStatus: Standby");
                PowerOnOff(false);
                nocontinue = true;
            }
            else if (!cockpit.DampenersOverride && NoPilotInput())
            {
                if (gdrive) TryWrite("<color=211,211,211,255>Drive: <color=160,160,0,160>Drifting", true);
                Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting");
                PowerOnOff(false);
                nocontinue = true;
            }
            else
            {
                string status = "<color=0,128,0,255>Active";
                if (NoPilotInput())
                {
                    status = "<color=139,0,0,255>Braking";
                }
                if (gdrive) TryWrite("<color=211,211,211,255>Drive: " + status, true);
                Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status);
                PowerOnOff(true);
            }

            if(spheres.Count == 0)
            {
                shieldString = "<color=139,0,0,255>Off";
                shield = 0;
            }
            if(gshield) TryWrite("\n<color=211,211,211,255>Shield: " + shieldString, true);
            if (blocks) TryWrite("\n<color=100,100,100,100>G " + gens.Count.ToString() + " / S " + spheres.Count.ToString() + " / A " + masses.Count.ToString(), true);
            if(runtime) TryWrite("\n<color=100,100,100,255>RT: " + Math.Round(averageRuntime, 2).ToString() + " / " + maxMs.ToString() + " ms", true);
            if(efficiency) TryWrite("\n<color=100,100,100,255>Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%", true);

            if (nocontinue) return;

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
                    sphere.GravityAcceleration = (float)-((moveInd.Z - velVect.Z) * IsFrontOrBack(sphere));
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
            lcd = null;
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

                GridTerminalSystem.GetBlocksOfType(allLCDs);

                foreach (IMyTextPanel lcdd in allLCDs)
                {
                    if (lcdd.CustomName.ToLower().Contains("gdrive") && lcdd.IsSameConstructAs(Me))
                    {
                        lcdd.ContentType = ContentType.TEXT_AND_IMAGE;
                        lcdd.BackgroundColor = Color.Black;
                        lcd = lcdd;
                    }
                }


                GridTerminalSystem.GetBlocksOfType(spheres);
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

                if (gens.Count == 0 && spheres.Count == 0)
                {
                    Echo("No working gravity generators were found.");
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
            if (massCenterVector == null) return 0;
            if ((-Vector3D.TransformNormal(sphere.GetPosition() - cockpit.CenterOfMass, MatrixD.Transpose(cockpit.WorldMatrix))).Z > massCenterVector.Z)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }
        public bool SpheresEqual()
        {
            int balanceNum = 0;
            foreach(IMyGravityGeneratorSphere sphere in spheres)
            {
                balanceNum = balanceNum + IsFrontOrBack(sphere);
            }
            if(balanceNum == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void TryWrite(string text, bool append = false)
        {
            if (lcd != null)
            {
                lcd.WriteText(text, append);
            }
        }
