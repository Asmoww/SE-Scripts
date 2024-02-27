        // GDV 1.22

        // the cockpit or remote control that is being controlled by a player will be automatically selected -
        // you can set one cockpit as main for it to have priority in case multiple are occupied and the script fails to detect the correct one 

        // some instructions for designing the gdrive
        // - artificial masses should be near the center of mass, otherwise the drive will rotate the ship
        // - spherical gens should only be placed in front of or behind the artificial masses
        // -- recommended to have an equal amount on both the front and back
        // -- otherwise gravity drive will be disabled while shield is active to prevent unwanted movement
        // - add at least one thruster for dampening to work

        // arguments you can use with hotbar
        // - shield is toggled between modes with the argument "shield" or directly with "push", "pull" or "off"
        // - gravity drive can be toggled on and off with "drive"

        // name an LCD "status lcd" to broadcast misc. info to hudlcd, multiple scripts can use the same status lcd

        // --Asmoww (asmoww on Discord)

        // general settings

        double maxMs = 0.3; // maximun allowed average ms per tick, 0.3 ms is allowed on most servers
        int refreshBlocksTick = 60; // how often to check for changes in blocks, in ticks. 60 ticks = 1 second
        int statusPriority = 1; // lower number = higher up on the status lcd, all scripts need to have a different number

        // gravity drive settings

        int lowestSpeed = 0; // dampening will be disabled below this speed (incase the ship keeps moving when trying to come to a stop)
        bool autoFieldSize = true;// automatically set the field size of the gravity generators
        bool keepDriveOn = true; // keep drive on while shield is on, will be disabled automatically if spherical gens aren't balanced correctly

        // which info to display on status lcd 

        bool runtime = true;
        bool gdrive = true;
        bool gshield = true;
        bool blocks = false;
        bool efficiency = false; // efficiency loss from natural gravity    


        // code below code below code below code below code below code below code below code below code below code below code below


        IMyShipController refBlock;
        List<IMyShipController> shipcontrollers = new List<IMyShipController>();
        List<IMyGravityGenerator> gens = new List<IMyGravityGenerator>();
        List<IMyGravityGeneratorSphere> spheres = new List<IMyGravityGeneratorSphere>();
        List<IMyArtificialMassBlock> masses = new List<IMyArtificialMassBlock>();
        Vector3I fieldMax, fieldMin;
        Vector3D massCenterVector = new Vector3D();
        int shield = 0;
        int drive = 1;
        static List<IMyTextPanel> allLCDs = new List<IMyTextPanel>();
        static List<IMyTextSurface> statusLCDs = new List<IMyTextSurface>();
        bool nocontinue = false;
        string scriptRunningChar = @" / ";

        public Program()
        {
            Echo("Loading...");
            GetBlocks(false);
            if(!waitTillErrorFixed) SetFieldSize();
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

        public void Main(string arg, UpdateType updateSource)
        {
            if (arg == "shield" || arg == "push" || arg == "pull" || arg == "off" || arg == "drive")
            {
                if (arg == "shield") arg = shield.ToString();
                if (arg == "drive") arg = "d" + drive.ToString();
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

            if (tickNum >= refreshBlocksTick)
            {
                GetBlocks(true);
                if (waitTillErrorFixed)
                {
                    tickNum = 0;
                    SendStatus("<color=255,0,0,255>GDRIVE ERROR: Check PB for more info!");
                    WriteStatus();
                }
                else SetFieldSize();
            }
            if (waitTillErrorFixed) return;

            Echo("Gravity generators: " + gens.Count.ToString() + " + " + spheres.Count.ToString() + "s");
            Echo("Artificial masses: " + masses.Count.ToString());
            Echo("Auto field size: " + autoFieldSize.ToString());
            Echo("Lowest speed: " + lowestSpeed.ToString());
            Echo("Efficiency: " + Math.Round((100 - (refBlock.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");
            Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");

            nocontinue = false;
            string effString = "";
            string driveString = "";

            if ((spheres.Count > 0 && shield != 0 && !SpheresEqual()) || (gens.Count == 0 && shield != 0) || drive == 0 || (shield != 0 && !keepDriveOn) || masses.Count == 0)
            {
                if (spheres.Count == 0 || shield == 0 || drive == 0 || masses.Count == 0)
                {
                    driveString = "<color=211,211,211,255>Drive: <color=139,0,0,255>Off";
                    Echo("Velocity: ---\nStatus: Off");
                }
                else
                {
                    driveString = "<color=211,211,211,255>Drive: <color=0,139,139,255>Shield";
                    Echo("Velocity: ---\nStatus: Shield");
                }
                PowerOnOff(false);
                nocontinue = true;
            }
            else if (Math.Round(refBlock.GetShipSpeed(), 2) == 0 && NoPilotInput())
            {
                driveString = "<color=211,211,211,255>Drive: <color=173,216,230,255>Standby";
                Echo("Velocity: ---\nStatus: Standby");
                PowerOnOff(false);
                nocontinue = true;
            }
            else if (!refBlock.DampenersOverride && NoPilotInput())
            {
                driveString = "<color=211,211,211,255>Drive: <color=160,160,0,160>Drifting";
                Echo("Velocity: " + Math.Round(refBlock.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting");
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
                driveString = "<color=211,211,211,255>Drive: " + status;
                Echo("Velocity: " + Math.Round(refBlock.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status);
                PowerOnOff(true);
            }

            if (spheres.Count == 0)
            {
                shieldString = "<color=139,0,0,255>Off";
                shield = 0;
            }
            if (tickNum % 10 == 0)
            {
                if (runtime) SendStatus("<color=100,100,100,255>GDV <color=70,70,70,255>" + Math.Round(averageRuntime, 2).ToString() + " ms" + scriptRunningChar);
                if (gdrive) SendStatus(driveString);
                if (gshield) SendStatus("<color=211,211,211,255>Shield: " + shieldString);
                if (efficiency) effString = "<color=100,100,100,255>" + Math.Round((100 - (refBlock.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%";
                if (blocks) SendStatus(effString + " <color=70,70,70,255>G " + gens.Count.ToString() + " / S " + spheres.Count.ToString() + " / A " + masses.Count.ToString());
                WriteStatus();
            }

            if (tickNum == refreshBlocksTick)
            {
                if (scriptRunningChar == @" / ") scriptRunningChar = @" \ ";
                else scriptRunningChar = @" / ";
                tickNum = 0;
            }

            if (nocontinue) return;

            velVect.X = Vector3D.TransformNormal(refBlock.GetShipVelocities().LinearVelocity, MatrixD.Transpose(refBlock.WorldMatrix)).X * IsZero(refBlock.MoveIndicator.X) * Convert.ToInt32(refBlock.DampenersOverride) * UnderLowestSpeed();
            velVect.Y = Vector3D.TransformNormal(refBlock.GetShipVelocities().LinearVelocity, MatrixD.Transpose(refBlock.WorldMatrix)).Y * IsZero(refBlock.MoveIndicator.Y) * Convert.ToInt32(refBlock.DampenersOverride) * UnderLowestSpeed();
            velVect.Z = Vector3D.TransformNormal(refBlock.GetShipVelocities().LinearVelocity, MatrixD.Transpose(refBlock.WorldMatrix)).Z * IsZero(refBlock.MoveIndicator.Z) * Convert.ToInt32(refBlock.DampenersOverride) * UnderLowestSpeed();
            moveInd = refBlock.MoveIndicator * 9.8f;

            foreach (IMyGravityGenerator gen in gens)
            {
                switch (refBlock.Orientation.TransformDirectionInverse(gen.Orientation.Up))
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

        List<string> statusList = new List<string>();
        static List<IMyProgrammableBlock> progBlocks = new List<IMyProgrammableBlock>();
        void SendStatus(string status)
        {
            statusList.Add(status);
        }
        void WriteStatus()
        {
            Me.CustomData = "statuspriority€" + statusPriority.ToString() + "€";
            foreach (string status in statusList)
            {
                Me.CustomData = Me.CustomData + status + "\n";
            }
            if (StatusHighestPriority())
            {
                foreach (IMyTextSurface lcd in statusLCDs)
                {
                    try { lcd.WriteText(StatusToWrite()); }
                    catch { }
                }
            }
            statusList.Clear();
        }
        bool StatusHighestPriority()
        {
            foreach (IMyProgrammableBlock prog in progBlocks)
            {
                if (prog.CustomData.StartsWith("statuspriority€") && prog.IsSameConstructAs(Me))
                {
                    int priority = -1;
                    int.TryParse(prog.CustomData.Split('€')[1], out priority);
                    if (priority < statusPriority)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        string StatusToWrite()
        {
            string tempToWrite = "";
            Dictionary<IMyProgrammableBlock, int> progBlocksTemp = new Dictionary<IMyProgrammableBlock, int>();
            IOrderedEnumerable<KeyValuePair<IMyProgrammableBlock, int>> sortedProgs;
            foreach (IMyProgrammableBlock prog in progBlocks)
            {
                if (!prog.CustomData.StartsWith("statuspriority€") || !prog.IsSameConstructAs(Me))
                {
                    progBlocks.Remove(prog);
                }
                else
                {
                    progBlocksTemp.Add(prog, Int32.Parse(prog.CustomData.Split('€')[1]));
                }
            }

            sortedProgs = (from entry in progBlocksTemp orderby entry.Value ascending select entry);
            foreach (KeyValuePair<IMyProgrammableBlock, int> prog in sortedProgs.ToList())
            {
                if (prog.Key.CustomData.StartsWith("statuspriority€"))
                {
                    tempToWrite = tempToWrite + prog.Key.CustomData.Split('€')[2];
                }
            }
            return tempToWrite;
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
            return refBlock.MoveIndicator.X == 0 && refBlock.MoveIndicator.Y == 0 && refBlock.MoveIndicator.Z == 0;
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
            if (refBlock.GetShipVelocities().LinearVelocity.Length() < lowestSpeed)
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
            refBlock = null;
            shipcontrollers.Clear();
            statusLCDs.Clear();
            gens.Clear();
            masses.Clear();
            spheres.Clear();
            progBlocks.Clear();
            fieldMax = Vector3I.Zero;
            fieldMin = Vector3I.Zero;
            massCenterVector = Vector3D.Zero;
        }
        public void GetMainShipController()
        {
            GridTerminalSystem.GetBlocksOfType(shipcontrollers);
            foreach (IMyShipController currentController in shipcontrollers)
            {
                if (currentController.IsUnderControl && currentController.CanControlShip && currentController.IsMainCockpit)
                {
                    refBlock = currentController;
                }
            }
            if (refBlock == null)
            {
                foreach (IMyShipController currentController in shipcontrollers)
                {
                    if (currentController.IsUnderControl && currentController.CanControlShip)
                    {
                        refBlock = currentController;
                    }
                }
            }
            if (refBlock == null && shipcontrollers.Any())
            {
                refBlock = shipcontrollers[0];
            }
        }
        public void GetBlocks(bool clear)
        {
            if (clear) Clear();
            waitTillErrorFixed = false;

            try
            {
                GetMainShipController();
                if(refBlock == null)
                {
                    Echo("No cockpit or remote control found, please add one.");
                    waitTillErrorFixed = true;
                }

                GridTerminalSystem.GetBlocksOfType(allLCDs);

                foreach (IMyTextPanel lcdd in allLCDs)
                {
                    if (lcdd.CustomName.ToLower().Contains("status") && lcdd.IsSameConstructAs(Me))
                    {
                        lcdd.ContentType = ContentType.TEXT_AND_IMAGE;
                        lcdd.BackgroundColor = Color.Black;
                        if (!lcdd.CustomData.Contains("hudlcd")) lcdd.CustomData = "hudlcd:-0.98:0.3";
                        statusLCDs.Add(lcdd);
                    }
                }

                GridTerminalSystem.GetBlocksOfType(progBlocks);
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
                    massCenterVector += -Vector3D.TransformNormal(mass.GetPosition() - refBlock.CenterOfMass, MatrixD.Transpose(refBlock.WorldMatrix));
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
            if ((-Vector3D.TransformNormal(sphere.GetPosition() - refBlock.CenterOfMass, MatrixD.Transpose(refBlock.WorldMatrix))).Z > massCenterVector.Z)
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
            foreach (IMyGravityGeneratorSphere sphere in spheres)
            {
                balanceNum = balanceNum + IsFrontOrBack(sphere);
            }
            if (balanceNum == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
