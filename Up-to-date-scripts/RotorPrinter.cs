        // use the following arguments:
        // start - starts the printer, turns on welders etc.
        // pause - pauses and continues welding
        // skip - skips layer, can be used after starting to get back to correct layer

        float weldingRPM = 0.07f; // how fast rotor spins when weldind, recommended 0 or a very low number like 0.03
        float idleRPM = 0.6f; // how fast the rotor should spin when not welding, recommended 0.4 or lower
        int weldTickDuration = 25; // 6 ticks = second, for how many ticks to use weldingRPM when welding is detected
        int passesPerLayer = 1; // how many times the printer should spin before starting next layer, 1 is probably enough if weldTickDuration is high enough, and weldingRPM low enough

        double maxMs = 0.3;
        bool runtime = true;
        int statusPriority = 3;

        string scriptNameVersion = "Rotor welder";
        int tickNum = 0;
        string scriptRunningChar = @" [ ";
        double averageRuntime = 0;
        int weldingTick = 0;
        int currentLayer = 1;
        double lastRotorAngle = 0;

        public Program()
        {
            Echo("Starting...");
            GetBlocks();
            Echo("Loaded!");
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        MyFixedPoint lastInvMass = 0;
        bool welding = false;
        bool newLayer = true;
        int currentPass = 0;
        bool printing = false;
        bool firstStart = true;
        bool pause = false;

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                switch (argument)
                {
                    case "start":
                        StartPrinting();
                        break;
                    case "pause":
                        printing = !printing;
                        pause = !pause;
                        if(!pause)
                        {
                            foreach(IMyShipWelder w in welders)
                            {
                                w.Enabled = true;
                            }
                        }
                        else
                        {
                            if (!pause)
                            {
                                foreach (IMyShipWelder w in welders)
                                {
                                    w.Enabled = false;
                                }
                            }
                        }
                        break;                  
                    case "skip":
                        if(printing) NewLayer();
                        break;
                }
            }            
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs / 10 * 0.01);
            if (averageRuntime > maxMs * 0.9) return;        

            if (rotor == null)
            {
                Echo("no rotor");
                return;
            }

            tickNum++;

            if (printing)
            {
                if (currentLayer >= 21)
                {
                    StopPrinting();
                    return;
                }
                // rotate rotor back to starting position 
                if (newLayer)
                {
                    rotor.LowerLimitDeg = 1;
                    if (rotor.Angle > 0.5) rotor.TargetVelocityRPM = -2f;
                    else rotor.TargetVelocityRPM = -0.5f;
                }

                // if pistons haven't retracted to wanted position yet or rotor isn't at starting position, return
                // otherwise start printing next layer
                if (WaitForPistons() || (Math.Abs(rotor.Angle) > (rotor.LowerLimitRad + 0.05) && newLayer))
                {
                    if (firstStart) SendStatus("Starting...");
                    else SendStatus("Changing layer...");
                }
                else if (newLayer)
                {
                    foreach (IMyShipWelder w in welders)
                    {
                        w.Enabled = true;
                    }
                    rotor.TargetVelocityRPM = idleRPM;
                    newLayer = false;
                    rotor.LowerLimitDeg = float.MinValue;
                    firstStart = false;
                }
                else
                {
                    weldingTick++;

                    // checks if welders are welding (and updates lastInvMass)
                    CheckIfWelding();

                    // adjust rotor speed if welding or not
                    if (welding)
                    {
                        rotor.TargetVelocityRPM = weldingRPM;
                        SendStatus("Welding...");
                    }
                    else
                    {
                        rotor.TargetVelocityRPM = idleRPM;
                        SendStatus("Rotating...");
                    }

                    // check if rotor has completed full loop
                    if (rotor.Angle < lastRotorAngle - 3)
                    {
                        currentPass += 1;
                        if (currentPass >= passesPerLayer) NewLayer();
                    }

                    // save rotors last angle for next tick
                    if (rotor.Angle > lastRotorAngle || newLayer) lastRotorAngle = rotor.Angle;
                }
            }
            else if(pause) SendStatus("Paused.");
            else SendStatus("Idle.");

            if (tickNum % 5 == 0) lastInvMass = CurrentInvMass();

            if (tickNum >= 20)
            {
                tickNum = 0;
                GetBlocks();
                if(!printing) StopPrinting();
            }
            // lcd and prog block info
            if (scriptRunningChar == @" / ") scriptRunningChar = @" \ ";
            else scriptRunningChar = @" / ";
            if (runtime) SendStatus("<color=150,150,150,255>" + scriptNameVersion + " <color=150,150,150,255>" + Math.Round(averageRuntime, 2).ToString() +" ms"+ scriptRunningChar);
            SendStatus("<color=150,150,150,255>Current layer: <color=200,200,200,255>" + Math.Round((rotor.Angle / (2 * Math.PI)) * 100, 1).ToString() + "%");
            SendStatus("<color=150,150,150,255>Layer number: <color=200,200,200,255>" + currentLayer.ToString());
            Echo(Math.Round(averageRuntime, 4).ToString() + "ms");
            WriteStatus();           
        }

        List<string> statusList = new List<string>();
        static List<IMyProgrammableBlock> progBlocks = new List<IMyProgrammableBlock>();
        static List<IMyTextSurface> statusLCDs = new List<IMyTextSurface>();
        static List<IMyShipToolBase> welders = new List<IMyShipToolBase>();
        static List<IMyTerminalBlock> inventoryBlocks = new List<IMyTerminalBlock>();
        static List<IMyPistonBase> pistons = new List<IMyPistonBase>();
        static IMyMotorStator rotor;

        void StartPrinting()
        {
            rotor.Enabled = true;
            rotor.LowerLimitDeg = float.MinValue;
            rotor.UpperLimitDeg = float.MaxValue;
            rotor.TargetVelocityRPM = 0f;
            foreach(IMyPistonBase p in pistons)
            {
                p.MinLimit = 10f;
                p.Velocity = 0.1f;
                p.Enabled = true;
            }
            printing = true;
            lastInvMass = CurrentInvMass();
            newLayer = true;
            foreach(IMyShipWelder w in welders)
            {
                w.Enabled = false;
            }
            firstStart = true;
        }

        void CheckIfWelding()
        {
            MyFixedPoint currentInvMass = CurrentInvMass();
            if (currentInvMass != lastInvMass)
            {
                weldingTick = 0;
                welding = true;
            }

            if (currentInvMass == lastInvMass && weldingTick >= weldTickDuration)
            {
                welding = false;
                weldingTick = 0;
                
            }           
        }

        void NewLayer()
        {
            firstStart = false;
            rotor.TargetVelocityRPM = 0;
            rotor.LowerLimitRad = -0.1f;
            currentLayer += 1;
            RetractPistons();
            newLayer = true;
            foreach (IMyShipWelder w in welders)
            {
                w.Enabled = false;
            }
            weldingTick = 0;
            tickNum = 0;
            lastInvMass = CurrentInvMass();
            currentPass = 0;
        }

        MyFixedPoint CurrentInvMass()
        {
            MyFixedPoint currentInvMass = 0;
            foreach (IMyTerminalBlock block in inventoryBlocks)
            {
                currentInvMass += block.GetInventory().CurrentMass;
            }
            return currentInvMass;
        }

        void StopPrinting()
        {
             printing = false;
            foreach(IMyShipWelder w in welders)
            {
                w.Enabled = false;
            }
            rotor.TargetVelocityRPM = 0f;
        }

        void RetractPistons()
        {
            foreach(IMyPistonBase p in pistons) 
            {
                p.MinLimit = (float)(10 - 0.5 * (currentLayer - 1));
                p.MaxLimit = (float)(10 - 0.5 * (currentLayer - 1));
                if(p.CurrentPosition < p.MinLimit) p.Velocity = 0.1f;
                else p.Velocity = -0.1f;
            }
        }

        bool WaitForPistons()
        {
            foreach (IMyPistonBase p in pistons)
            {
                if (Math.Abs(p.CurrentPosition-p.MinLimit) > 0.05) return true;
            }
            return false;
        }

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

        string ColorToColor(Color color)
        {
            return $"<color={color.R},{color.G},{color.B},{color.A}>";
        }

        void GetBlocks()
        {
            statusLCDs.Clear();
            progBlocks.Clear();
            pistons.Clear();
            rotor = null;
            welders.Clear();
            inventoryBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, SortBlocks);
        }

        bool SortBlocks(IMyTerminalBlock block)
        {
            if (block.HasInventory)
            {
                if (block is IMyReactor) Echo("reactor");
                else inventoryBlocks.Add(block);
            }
            else if (block is IMyPistonBase)
            {
                pistons.Add(block as IMyPistonBase);
            }
            else if (block is IMyMotorStator)
            {
                rotor = block as IMyMotorStator;
            }
            else if (block is IMyShipToolBase)
            {
                IMyShipToolBase welder = block as IMyShipToolBase;
                welders.Add(welder);
            }
            if (!block.IsSameConstructAs(Me)) return false;
            if (block.CustomName.ToLower().Contains("status") && block is IMyTextPanel)
            {
                IMyTextPanel lcd = block as IMyTextPanel;
                lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                lcd.BackgroundColor = Color.Black;
                statusLCDs.Add(lcd);
                if (!lcd.CustomData.Contains("hudlcd")) lcd.CustomData = "hudlcd:-0.98:0.3";
                return false;
            }
            else if (block is IMyProgrammableBlock)
            {
                IMyProgrammableBlock progBlock = block as IMyProgrammableBlock;
                progBlocks.Add(progBlock);
            }
            else if (block is IMyShipToolBase)
            {
                IMyShipToolBase welder = block as IMyShipToolBase;
                welders.Add(welder);
            }           
            return false;
        }
