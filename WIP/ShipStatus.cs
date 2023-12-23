#region code 
        bool init = false;
        bool firstRun = true;
        static string[,] gridMatrix = new string[0,0];
        static bool[,,] originalBlocks = new bool[0, 0, 0];
        static List<IMyTextSurface> shipLcds = new List<IMyTextSurface>();
        static List<IMyCockpit> cockpits = new List<IMyCockpit>();
        static List<IMyTerminalBlock> blocksToScan = new List<IMyTerminalBlock>();
        Vector3I boundBox = Vector3I.Zero;
        IMyCockpit refCockpit;
        double averageRuntime = 0;
        int lastScanRow = 0;


        /*static char backgroundColor = Rgb(1, 1, 1);
        static char hullColor = Rgb(5,5,5);
        static char tankColor = Rgb(5,2,1);
        static char reactorColor = Rgb(0,5,0);
        static char connectorColor = Rgb(0,0,5);*/
        static string hullColor = "<color=255,255,255,255>██";
        static string backgroundColor = "  ";
        static string tankColor = "<color=255,100,100,255>██";
        static string reactorColor = "<color=0,255,0,255>██";

        static char Rgb(byte r, byte g, byte b) { return (char)(0xe100 + (r << 6) + (g << 3) + b); }

        public Program()
        {
            Echo("Starting...");
            GetBlocks();
            lastScanRow = boundBox.Z + 1;
            Echo("Loaded!");
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        int ticknum = 0;
        public void Main(string argument, UpdateType updateSource)
        {
            ticknum++;
            if (!init)
            {
                InitScript(); // scan all blocks one row per tick to prevent overheating
                return;
            }
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs * 0.01);
            if (ticknum%10==0)
            {
                GetBlocks(); // update block lists
                ScanShape(); // get ship shape (scans all blocks)
                FillGridMatrix(); // fills empty spots with background color
                UpdateBlocks(); // updates important terminal blocks (tanks, reactors etc)
                WriteToLcds(); // updates lcd
                ticknum = 0;
            }

            Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString()+ " ms");
            Echo("Width: " + boundBox.X.ToString());
            Echo("Height: " + boundBox.Y.ToString());
            Echo("Length: " + boundBox.Z.ToString());
            
        }
        void UpdateBlocks() // update important terminal blocks 
        {
            foreach (IMyTerminalBlock block in blocksToScan)
            {
                string color = "";
                if (block is IMyGasTank) color = tankColor;
                if (block is IMyReactor) color = reactorColor;
                for (int length = CorrectRotation(block.Min).Z; length <= CorrectRotation(block.Max).Z; length++)
                {
                    for (int width = CorrectRotation(block.Min).X; width <= CorrectRotation(block.Max).X; width++)
                    {
                        gridMatrix[Math.Abs(Math.Abs((length - CorrectRotation(Me.CubeGrid.Max).Z)) - boundBox.Z), Math.Abs(width - CorrectRotation(Me.CubeGrid.Max).X)] = color;
                    }
                }
            }
        }
        void InitScript()
        {
            if (ticknum <= boundBox.Z)
            {
                Echo("Loading blocks... " + Math.Round(((decimal)ticknum / (decimal)boundBox.Z) * 100, 1) + "%");
                ScanShape();
            }
            else
            {
                init = true;
                ticknum = 0;
            }
        }
        void FillGridMatrix() // fill empty spots with background color
        {
            for(int z = 0; z <= boundBox.Z; z++)
            {
                for (int x = 0; x <= boundBox.X; x++)
                {
                    if (gridMatrix[z,x] == null)
                    {
                        gridMatrix[z,x] = backgroundColor;
                    }
                }
            }
        }
        void WriteToLcds()
        {
            foreach (IMyTextSurface lcd in shipLcds)
            {
                List<string> tempList = new List<string>();
                for (int z = 0; z <= boundBox.Z; z++)
                {
                    string tempString = "";
                    for (int x = 0; x <= boundBox.X; x++)
                    {
                        tempString = tempString + gridMatrix[z, x];
                    }
                    tempList.Add(tempString);
                }
                string tempCompleteString = "";
                foreach (string tempString in tempList)
                {
                    tempCompleteString = tempCompleteString + tempString + "\n";
                }
                lcd.WriteText(tempCompleteString);
            }
        }
        void GetBlocks()
        { 
            // clear lists here
            shipLcds.Clear();
            cockpits.Clear();      
            blocksToScan.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, SortBlocks);
            foreach (IMyCockpit cockpit in cockpits)
            {
                if (cockpit.IsMainCockpit)
                {
                    refCockpit = cockpit;
                }
                else if (cockpit.IsUnderControl)
                {
                    refCockpit = cockpit;
                }
                else
                {
                    refCockpit = cockpits[0];
                }
            }
            boundBox = Vector3I.Abs(CorrectRotation(Me.CubeGrid.Max - Me.CubeGrid.Min));
            Vector3I realBoundBox = Vector3I.Abs(Me.CubeGrid.Max - Me.CubeGrid.Min);
            if (firstRun)
            {
                Array.Clear(gridMatrix, 0, gridMatrix.Length);
                gridMatrix = new string[boundBox.Z + 1, boundBox.X + 1];
                originalBlocks = new bool[realBoundBox.X+1, realBoundBox.Y+1, realBoundBox.Z+1];

                firstRun = false;
            }
        }

        bool SortBlocks(IMyTerminalBlock block)
        {
            if (!block.IsSameConstructAs(Me)) return false;
            if (block.CustomName.ToLower().Contains("ship") && block is IMyTextPanel)
            {
                IMyTextPanel lcd = block as IMyTextPanel;
                lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                lcd.BackgroundColor = Color.Black;
                shipLcds.Add(lcd);
            }
            if(block is IMyGasTank && block.DetailedInfo.Contains("Hydrogen Tank") && !block.DetailedInfo.Contains("Small"))
            {
                blocksToScan.Add(block as IMyGasTank);
            }
            if(block is IMyReactor)
            {
                blocksToScan.Add(block as IMyReactor);
            }
            if(block is IMyCockpit)
            {
                cockpits.Add(block as IMyCockpit);
            }
            return false;
        }

        void ScanShape() // scan all blocks to get ship shape
        {          
            if (lastScanRow > boundBox.Z)
            {
                lastScanRow = Me.CubeGrid.Min.Z;
            }
            for (int width = Me.CubeGrid.Min.X; width < Me.CubeGrid.Max.X+1; width++)
            {
                for (int heigth = Me.CubeGrid.Min.Y; heigth < Me.CubeGrid.Max.Y+1; heigth++)
                {
                    if (Me.CubeGrid.CubeExists(new Vector3I(width, heigth, lastScanRow)))
                    {
                        //Echo(new Vector3I(width, heigth, length).ToString());
                        //Echo(Math.Abs((Math.Abs(CorrectRotation(new Vector3I(width, heigth, length) - Me.CubeGrid.Max).Z) - boundBox.Z)).ToString() + "   " + Math.Abs(CorrectRotation(new Vector3I(width, heigth, length) - Me.CubeGrid.Max).X).ToString());
                        if (!init)
                        {
                            gridMatrix[Math.Abs(Math.Abs(CorrectRotation(new Vector3I(width, heigth, lastScanRow) - Me.CubeGrid.Max).Z) - boundBox.Z), Math.Abs(CorrectRotation(new Vector3I(width, heigth, lastScanRow) - Me.CubeGrid.Max).X)] = hullColor;
                            originalBlocks[Math.Abs(width-Me.CubeGrid.Max.X), Math.Abs(heigth - Me.CubeGrid.Max.Y), Math.Abs(lastScanRow - Me.CubeGrid.Max.Z)] = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            lastScanRow++;
        }

         Vector3I CorrectRotation(Vector3I vector)
        {
            Vector3I tempVector = new Vector3I(0,0,0);
            switch (refCockpit.Orientation.Up)
            {
                case Base6Directions.Direction.Up:
                    tempVector.Y = vector.Y;
                    break;
                case Base6Directions.Direction.Down:
                    tempVector.Y = -vector.Y;
                    break;
                case Base6Directions.Direction.Right:
                    tempVector.Y = vector.X;
                    break;
                case Base6Directions.Direction.Left:
                    tempVector.Y = -vector.X;
                    break;
                case Base6Directions.Direction.Backward:
                    tempVector.Y = -vector.Z;
                    break;
                case Base6Directions.Direction.Forward:
                    tempVector.Y = vector.Z;
                    break;
            }
            switch (refCockpit.Orientation.Left)
            {
                case Base6Directions.Direction.Up:
                    tempVector.X = vector.Y;
                    break;
                case Base6Directions.Direction.Down:
                    tempVector.X = -vector.Y;
                    break;
                case Base6Directions.Direction.Right:
                    tempVector.X = -vector.X;
                    break;
                case Base6Directions.Direction.Left:
                    tempVector.X = vector.X;
                    break;
                case Base6Directions.Direction.Backward:
                    tempVector.X = -vector.Z;
                    break;
                case Base6Directions.Direction.Forward:
                    tempVector.X = vector.Z;
                    break;
            }
            switch (refCockpit.Orientation.Forward)
            {
                case Base6Directions.Direction.Up:
                    tempVector.Z = vector.Y;
                    break;
                case Base6Directions.Direction.Down:
                    tempVector.Z = -vector.Y;
                    break;
                case Base6Directions.Direction.Right:
                    tempVector.Z = vector.X;
                    break;
                case Base6Directions.Direction.Left:
                    tempVector.Z = -vector.X;
                    break;
                case Base6Directions.Direction.Backward:
                    tempVector.Z = -vector.Z;
                    break;
                case Base6Directions.Direction.Forward:
                    tempVector.Z = vector.Z;
                    break;
            }
            return tempVector;
        }
        #endregion
