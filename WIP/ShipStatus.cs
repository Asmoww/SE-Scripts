#region code 
        bool init = false;
        bool firstRun = true;
        static char[,] gridMatrix = new char[0,0];
        static List<IMyTextSurface> shipLcds = new List<IMyTextSurface>();
        static List<IMyGasTank> tanks = new List<IMyGasTank>();
        static List<IMyCockpit> cockpits = new List<IMyCockpit>();
        Vector3I boundBox = Vector3I.Zero;
        IMyCockpit refCockpit;
        double averageRuntime = 0;
        int lastScanRow = 0;

        public Program()
        {
            Echo("Starting...");
            GetBlocks();
            lastScanRow = boundBox.Z + 1;
            Echo("Loaded!");
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
        int ticknum = 0;
        public void Main(string argument, UpdateType updateSource)
        {
            ticknum++;
            if (!init)
            {
                InitScript();
                return;
            }
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs * 0.01);
            if (ticknum%2==0)
            {
                ScanShape();
                FillGridMatrix();
            }
            if (ticknum%10==0)
            {
                GetBlocks();
                WriteToLcds();
                ticknum = 0;
            }

            Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString()+ " ms");

            Echo("Width: " + boundBox.X.ToString());
            Echo("Height: " + boundBox.Y.ToString());
            Echo("Length: " + boundBox.Z.ToString());

            foreach (IMyGasTank tank in tanks)
            {
                //Echo(Math.Abs(CorrectRotation(tank.Position - Me.CubeGrid.Max).Z - boundBox.Z).ToString() +" --- "+ CorrectRotation(tank.Position - Me.CubeGrid.Max).X.ToString());
                gridMatrix[Math.Abs(Math.Abs(CorrectRotation(tank.Position - Me.CubeGrid.Max).Z) - boundBox.Z), Math.Abs(CorrectRotation(tank.Position - Me.CubeGrid.Max).X)] = 'X';
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
        void FillGridMatrix()
        {
            for(int z = 0; z <= boundBox.Z; z++)
            {
                for (int x = 0; x <= boundBox.X; x++)
                {
                    if (gridMatrix[z,x] == 0)
                    {
                        gridMatrix[z,x] = ' ';
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
            tanks.Clear();
            cockpits.Clear();          
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
            if(firstRun)
            {
                Array.Clear(gridMatrix, 0, gridMatrix.Length);
                gridMatrix = new char[boundBox.Z + 1, boundBox.X + 1];
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
                tanks.Add(block as IMyGasTank);
            }
            if(block is IMyCockpit)
            {
                cockpits.Add(block as IMyCockpit);
            }
            return false;
        }

        void ScanShape()
        {          
            if (lastScanRow > boundBox.Z)
            {
                lastScanRow = 0;
            }
            for (int width = Me.CubeGrid.Min.X; width < Me.CubeGrid.Max.X+1; width++)
            {
                for (int heigth = Me.CubeGrid.Min.Y; heigth < Me.CubeGrid.Max.Y+1; heigth++)
                {
                    if (Me.CubeGrid.CubeExists(new Vector3I(width, heigth, lastScanRow)))
                    {
                        //Echo(new Vector3I(width, heigth, length).ToString());
                        //Echo(Math.Abs((Math.Abs(CorrectRotation(new Vector3I(width, heigth, length) - Me.CubeGrid.Max).Z) - boundBox.Z)).ToString() + "   " + Math.Abs(CorrectRotation(new Vector3I(width, heigth, length) - Me.CubeGrid.Max).X).ToString());
                        gridMatrix[Math.Abs(Math.Abs(CorrectRotation(new Vector3I(width, heigth, lastScanRow) - Me.CubeGrid.Max).Z)-boundBox.Z), Math.Abs(CorrectRotation(new Vector3I(width, heigth, lastScanRow) - Me.CubeGrid.Max).X)] = '+';
                        continue;
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
