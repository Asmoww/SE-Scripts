        // AIM version 1.0
        // Aimbot based off of Skunkbot (Grunkbot)

        // Due to Keen being stupid, having a lot of gyros quickly eats runtime
        // This is why you can set the max number of gyros the script is allowed to use
        // I would recommend 100 as the absolute max

        // Blocks needed: cockpit, weaponcore turret (or doppler radar)

        // Name an LCD "status lcd" to broadcast misc. info to hudlcd, multiple scripts can use the same status lcd

        // general settings
        double maxMs = 0.3; // throttle the script if runtime exceeds this
        int statusPriority = 3; // lower number = higher up on the status lcd, all scripts need to have a different number

        // which info to display on status lcd 
        bool runtime = true;
        bool aimAssist = true;

        // static weapon's projectile velocity, in m/s 
        const double projectileVelocity = 800;

        // max number of gyros to use
        int maxGyros = 60;



        // code below code below code below code below code below code below code below code below code below code below code below
        // code below code below code below code below code below code below code below code below code below code below code below
        // code below code below code below code below code below code below code below code below code below code below code below



        string scriptNameVersion = "AIM";
        public static WcPbApi wcapi = new WcPbApi();
        int tickNum = 0;
        string scriptRunningChar = @" / ";
        double averageRuntime = 0;
        bool standby = false;
        bool error = false;

        const double DEF_SMALL_GRID_P = 40; // The default proportional gain of small grid gyroscopes
        const double DEF_SMALL_GRID_I = 0; // The default integral gain of small grid gyroscopes
        const double DEF_SMALL_GRID_D = 13; // The default derivative gain of small grid gyroscopes

        const double DEF_BIG_GRID_P = 15; // The default proportional gain of large grid gyroscopes
        const double DEF_BIG_GRID_I = 0; // The default integral gain of large grid gyroscopes
        const double DEF_BIG_GRID_D = 7; // The default derivative gain of large grid gyroscopes

        double AIM_P = 0; // The proportional gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
        double AIM_I = 0; // The integral gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
        double AIM_D = 0; // The derivative gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
        double AIM_LIMIT = 60; // Limit value of both yaw and pitch combined

        double INTEGRAL_WINDUP_LIMIT = 0; // Integral value limit to minimize integral windup. Zero means no limit

        //------------------------------ Below Is Main Script Body ------------------------------

        IMyShipController aimPointBlock;
        List<IMyShipController> cockpits = new List<IMyShipController> ();

        GyroControl gyroControl;
        PIDController yawController;
        PIDController pitchController;
        PIDController rollController;

        MatrixD refWorldMatrix;
        MatrixD refLookAtMatrix;

        Vector3D aimDirection;
        Vector3D lastTargetSpeed;
        Vector3D newAimDirection;

        bool botactivated = false;
        bool init = false;

        Random rnd = new Random();

        const double DIST_FACTOR = Math.PI / 2;
        const double RPM_FACTOR = 1800 / Math.PI;
        const double ACOS_FACTOR = 180 / Math.PI;
        const float GYRO_FACTOR = (float)(Math.PI / 30);
        const double RADIAN_FACTOR = Math.PI / 180;

        const float SECOND = 30f;

        List<IMyGyro> gyros = new List<IMyGyro>();

        public Program()
        {
            Echo("Starting...");
            try
            {
                wcapi.Activate(Me);
            }
            catch
            {
                Echo("Weaponcore API failed to activate.");
            }
            GetBlocks();
            Echo("Loaded!");
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                switch (argument)
                {
                    case "aim":
                        botactivated = !botactivated;
                        break;
                }
            }
            standby = false;
            tickNum++;
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs * 0.01);
            if (averageRuntime > maxMs * 0.9)
            {
                return;
            }
            if (!error)
            {
                if (botactivated && aimPointBlock.IsUnderControl && tickNum % 2 == 0)
                {
                    Aimbot();
                }
                if (!botactivated || standby || !aimPointBlock.IsUnderControl)
                {
                    gyroControl.ResetGyro();
                    gyroControl.SetGyroOverride(false);
                }
                Echo(Math.Round(averageRuntime, 4).ToString() + "ms");
                if (tickNum % 10 == 0)
                {
                    if (runtime) SendStatus("<color=100,100,100,255>" + scriptNameVersion + " <color=70,70,70,255>" + Math.Round(averageRuntime, 2).ToString() + " ms" + scriptRunningChar);
                    string aimString = "<color=139,0,0,255>Off";
                    if (botactivated) aimString = "<color=0,128,0,255>Locked";
                    if (standby) aimString = "<color=173,216,230,255>Standby";
                    if (aimAssist) SendStatus("<color=211,211,211,255>Aimbot: " + aimString);
                    WriteStatus();
                }
            }
            if(tickNum == 60)
            {
                if (scriptRunningChar == @" / ") scriptRunningChar = @" \ ";
                else scriptRunningChar = @" / ";
                GetBlocks();
                tickNum = 0;
            }
        }

        List<string> statusList = new List<string>();
        static List<IMyProgrammableBlock> progBlocks = new List<IMyProgrammableBlock>();
        static List<IMyTextSurface> statusLCDs = new List<IMyTextSurface>();

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
     
        void Aimbot()
        {
            MyDetectedEntityInfo entityInfo = wcapi._getAiFocus(Me.CubeGrid.EntityId, 0);
            if (entityInfo.EntityId > 0)
            {
                refWorldMatrix = aimPointBlock.WorldMatrix;
                refLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, refWorldMatrix.Forward, refWorldMatrix.Up);
                Vector3D targetSpeed = entityInfo.Velocity;
                Vector3D mySpeed = Me.CubeGrid.LinearVelocity;
                Vector3D relativeVelocity = targetSpeed - mySpeed;
                Vector3D targetAcceleration = (targetSpeed - lastTargetSpeed) * SECOND;
                double targetDistance = Math.Round(Vector3D.Distance(entityInfo.BoundingBox.Center, aimPointBlock.GetPosition()), 0);
                double time = (targetDistance / projectileVelocity);
                double oldTime;
                aimDirection = entityInfo.BoundingBox.Center - refWorldMatrix.Translation + relativeVelocity * time + 0.5 * Math.Pow(time, 2) * targetAcceleration;
                newAimDirection = aimDirection;
                do
                {
                    oldTime = time;
                    aimDirection = entityInfo.BoundingBox.Center;
                    aimDirection = aimDirection - refWorldMatrix.Translation + 1.0065 * relativeVelocity * time + 0.5 * time * time * targetAcceleration;
                    time = (aimDirection.Length() / projectileVelocity);
                } while (Math.Abs(time - oldTime) > (0.01 * oldTime));
                lastTargetSpeed = targetSpeed;
                aimDirection = Vector3D.Normalize(Vector3D.TransformNormal(aimDirection, refLookAtMatrix));
                AimAtTarget(aimDirection);
            }
            else
            {
                standby = true;
            }
        }

        string ColorToColor(Color color)
        {
            return $"<color={color.R},{color.G},{color.B},{color.A}>";
        }      

        void GetBlocks()
        {
            statusLCDs.Clear();
            progBlocks.Clear();
            aimPointBlock = null;
            gyros.Clear();
            cockpits.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, SortBlocks);
            error = false;
            if (cockpits.Count() == 0)
            {
                error = true;
                Echo("No cockpit found, please add one!");
            }
            if (gyros.Count() == 0)
            {
                error = true;
                Echo("No gyroscopes were found, please add some!");
            }
            if (error)
            {
                SendStatus("<color=255,0,0,255>AIMBOT ERROR: Check PB for more info!");
                return;
            }
            aimPointBlock = GetControlledCockpit();
            if (aimPointBlock == null) aimPointBlock = cockpits[0];
            refWorldMatrix = aimPointBlock.WorldMatrix;
            gyroControl = new GyroControl(gyros, ref refWorldMatrix);
            refLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, refWorldMatrix.Forward, refWorldMatrix.Up);
            InitPIDControllers();
        }

        bool SortBlocks(IMyTerminalBlock block)
        {
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
            else if (block is IMyGyro)
            {
                IMyGyro gyroBlock = block as IMyGyro;
                if(gyros.Count < maxGyros) gyros.Add(gyroBlock);
            }
            else if (block is IMyShipController)
            {
                IMyShipController cockpit = block as IMyShipController;
                cockpits.Add(cockpit);
            }
            return false;
        }

        IMyShipController GetControlledCockpit()
        {
            foreach (IMyShipController cockpit in cockpits)
            { 
                if(cockpit.IsUnderControl) return cockpit;
            }
            return null;
        }

        void AimAtTarget(Vector3D targetVector)
        {
            Vector3D yawVector = new Vector3D(targetVector.GetDim(0), 0, targetVector.GetDim(2));
            Vector3D pitchVector = new Vector3D(0, targetVector.GetDim(1), targetVector.GetDim(2));
            yawVector.Normalize();
            pitchVector.Normalize();
            double yawInput = Math.Acos(yawVector.Dot(Vector3D.Forward)) * GetMultiplierSign(targetVector.GetDim(0));
            double pitchInput = Math.Acos(pitchVector.Dot(Vector3D.Forward)) * GetMultiplierSign(targetVector.GetDim(1));
            yawInput = yawController.Filter(yawInput, 2);
            pitchInput = pitchController.Filter(pitchInput, 2);
            if (Math.Abs(yawInput) + Math.Abs(pitchInput) > AIM_LIMIT)
            {
                double adjust = AIM_LIMIT / (Math.Abs(yawInput) + Math.Abs(pitchInput));
                yawInput *= adjust;
                pitchInput *= adjust;
            }
            gyroControl.SetGyroOverride(true);
            gyroControl.SetGyroRates((float)yawInput, (float)pitchInput, aimPointBlock.RollIndicator * -30);
        }

        int GetMultiplierSign(double value)
        {
            return (value < 0 ? -1 : 1);
        }

        void InitPIDControllers()
        {
            if (AIM_P + AIM_I + AIM_D < 0.001)
            {
                if (Me.CubeGrid.ToString().Contains("Large"))
                {
                    AIM_P = DEF_BIG_GRID_P;
                    AIM_I = DEF_BIG_GRID_I;
                    AIM_D = DEF_BIG_GRID_D;
                }
                else
                {
                    AIM_P = DEF_SMALL_GRID_P;
                    AIM_I = DEF_SMALL_GRID_I;
                    AIM_D = DEF_SMALL_GRID_D;
                    AIM_LIMIT *= 2;
                }
            }
            yawController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
            pitchController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
            rollController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
        }

        public class GyroControl
        {
            string[] profiles = {
            "Yaw",
            "Yaw",
            "Pitch",
            "Pitch",
            "Roll",
            "Roll"
            };

            List<IMyGyro> gyros;

            private byte[] gyroYaw;
            private byte[] gyroPitch;
            private byte[] gyroRoll;

            public GyroControl(List<IMyGyro> newGyros, ref MatrixD refWorldMatrix)
            {
                gyros = new List<IMyGyro>(newGyros.Count);

                gyroYaw = new byte[newGyros.Count];
                gyroPitch = new byte[newGyros.Count];
                gyroRoll = new byte[newGyros.Count];

                int index = 0;
                foreach (IMyGyro block in newGyros)
                {
                    IMyGyro gyro = block as IMyGyro;
                    if (gyro != null)
                    {
                        gyroYaw[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Up));
                        gyroPitch[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Left));
                        gyroRoll[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Forward));

                        gyros.Add(gyro);

                        index++;
                    }
                }
            }

            public byte SetRelativeDirection(Base6Directions.Direction dir)
            {
                switch (dir)
                {
                    case Base6Directions.Direction.Up:
                        return 0;
                    case Base6Directions.Direction.Down:
                        return 1;
                    case Base6Directions.Direction.Left:
                        return 2;
                    case Base6Directions.Direction.Right:
                        return 3;
                    case Base6Directions.Direction.Forward:
                        return 5;
                    case Base6Directions.Direction.Backward:
                        return 4;
                }
                return 0;
            }
            public void SetGyroRates(float yawRate, float pitchRate, float rollRate)   
            {
                for (int i = 0; i < gyros.Count; i++)
                {
                    byte Yindex = gyroYaw[i];
                    gyros[i].SetValue(profiles[Yindex], (Yindex % 2 == 0 ? yawRate : -yawRate) * MathHelper.RadiansPerSecondToRPM);
                    byte Pindex = gyroPitch[i];
                    gyros[i].SetValue(profiles[Pindex], (Pindex % 2 == 0 ? pitchRate : -pitchRate) * MathHelper.RadiansPerSecondToRPM);
                    byte Rindex = gyroRoll[i];
                    gyros[i].SetValue(profiles[Rindex], (Rindex % 2 == 0 ? rollRate : -rollRate) * MathHelper.RadiansPerSecondToRPM);
                }
            }

            public void SetGyroOverride(bool bOverride)
            {
                foreach (IMyGyro gyro in gyros)
                {
                    if (gyro.GyroOverride != bOverride)
                    {
                        gyro.GyroOverride = bOverride;
                    }
                }
            }

            public void ResetGyro()
            {
                foreach (IMyGyro gyro in gyros)
                {
                    gyro.Yaw = 0f;
                    gyro.Pitch = 0f;
                    gyro.Roll = 0f;
                }
            }
        }

        public class PIDController
        {
            double integral;
            double lastInput;

            double gain_p;
            double gain_i;
            double gain_d;
            double upperLimit_i;
            double lowerLimit_i;
            double second;

            public PIDController(double pGain, double iGain, double dGain, double iUpperLimit = 0, double iLowerLimit = 0, float stepsPerSecond = 60f)
            {
                gain_p = pGain;
                gain_i = iGain;
                gain_d = dGain;
                upperLimit_i = iUpperLimit;
                lowerLimit_i = iLowerLimit;
                second = stepsPerSecond;
            }


            public double Filter(double input, int round_d_digits)
            {
                double roundedInput = Math.Round(input, round_d_digits);

                integral = integral + (input / second);
                integral = (upperLimit_i > 0 && integral > upperLimit_i ? upperLimit_i : integral);
                integral = (lowerLimit_i < 0 && integral < lowerLimit_i ? lowerLimit_i : integral);

                double derivative = (roundedInput - lastInput) * second;
                lastInput = roundedInput;

                return (gain_p * input) + (gain_i * integral) + (gain_d * derivative);
            }

            public void Reset()
            {
                integral = lastInput = 0;
            }
        }

        public class WcPbApi
        {
            public Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Func<long, bool> _hasGridAi;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
            private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;

            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception($"WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;

                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);

                return true;
            }

            private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
            {
                if (delegates == null)
                {
                    field = null;
                    return;
                }
                Delegate del;
                if (!delegates.TryGetValue(name, out del))
                    throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
                field = del as T;
                if (field == null)
                    throw new Exception(
                        $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
            }
            public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pbBlock, collection);
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
            public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;
        }
