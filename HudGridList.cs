
        //Lists nearby grids to LCDs, takes info from weaponcore api
        //meant to work with hudlcd plugin
        //name LCDs "Friend LCD" and "Target LCD"
        //--Asmoww
        double maxMs = 0.4;
        bool sortByDistance = true; //sort by threat if disabled 
        int closeDistance = 5000; //at what distance should colors start be applied to distance number
        bool approachWarning = true; //warn for approaching grids with sound block
        int approachDistance = 1500; //distance in meters, warn if grid is approaching in specified distance
        int approachSpeed = 3; //speed in m/s, if approaching faster, warn
        string warningSound = "SoundBlockAlert2";
        int maxNameLenght = 25; //how long grid names are allowed to be displayed without cutting them off
        Color friendColor = Color.Green;
        Color neutralColor = Color.LightBlue;
        Color enemyColor = Color.IndianRed;
        Color approachColor = Color.Yellow;
        Color targetingColor = Color.Orange;

        public static WcPbApi wcapi = new WcPbApi();
        Dictionary<MyDetectedEntityInfo, float> wcTargets = new Dictionary<MyDetectedEntityInfo, float>();
        List<MyDetectedEntityInfo> wcObstructions = new List<MyDetectedEntityInfo>();
        Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
        Dictionary<long, double> prevDistances = new Dictionary<long, double>();
        static MyDetectedEntityInfo currentTarget;
        bool approaching = false;
        bool soundPlayed = false;
        int tickNum = 0;
        double averageRuntime = 0;
        static double tickSpeed = 0.1667; //seconds per tick

        struct TargetData
        {
            public MyDetectedEntityInfo Info;
            public long Targeting;
            public bool MyTarget;
            public double Distance;
            public double ApproachSpeed;
            public float Threat;
            public Color Color;
            public Color DistanceColor;

            public TargetData(MyDetectedEntityInfo info, long targeting = 0, bool myTarget = false, double distance = 0, double approachSpeed = 0, float threat = 0, Color color = default(Color), Color distanceColor = default(Color))
            {
                Info = info;
                Targeting = targeting;
                MyTarget = myTarget;
                Distance = distance;
                ApproachSpeed = approachSpeed;
                Threat = threat;
                Color = color;
                DistanceColor = distanceColor;
            }
        }

        public Program()
        {
            GetBlocks();
            try
            {
                wcapi.Activate(Me);
            }
            catch
            {
                Echo("Weaponcore API failed to activate.");
            }
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            tickNum++;
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs / 10 * 0.01);
            Echo(Math.Round(averageRuntime, 4).ToString() + "ms");
            Echo((friendLCDs.Count + targetLCDs.Count).ToString() + " LCDs");
            switch (sortByDistance)
            {
                case true:
                    Echo("Sorting by distance");
                    break;
                case false:
                    Echo("Sorting by threat level");
                    break;
            }
            if (tickNum == 10)
            {
                GetBlocks();
                tickNum = 0;
            }
            if (averageRuntime > maxMs * 0.9)
            {
                return;
            }
            if (targetLCDs.Count + friendLCDs.Count > 0)
            {
                UpdateLCDs();
            }
        }

        Dictionary<String, double> targetOutput = new Dictionary<String, double>();
        Dictionary<String, double> friendOutput = new Dictionary<String, double>();

        static List<IMyTextPanel> allLCDs = new List<IMyTextPanel>();
        static List<IMyTextSurface> targetLCDs = new List<IMyTextSurface>();
        static List<IMyTextSurface> friendLCDs = new List<IMyTextSurface>();

        void UpdateLCDs()
        {
            GetAllTargets();
            ClearAll();

            foreach (var obj in targetDataDict)
            {
                try
                {
                    var target = obj.Value;
                    double sorter = target.Threat;
                    int myTargetPriority = 100;
                    string type;
                    string warning = "";
                    switch (target.Info.Type)
                    {
                        case MyDetectedEntityType.CharacterHuman:
                            warning = "Suit";
                            break;
                        case MyDetectedEntityType.SmallGrid:
                            warning = "S";
                            break;
                        default:
                            warning = "";
                            break;
                    }
                    if (sortByDistance)
                    {
                        sorter = target.Distance;
                        myTargetPriority = -1;
                    }
                    if (target.Color == targetingColor)
                        warning = "@"+warning;

                    if (target.MyTarget)
                    {
                        warning = ">"+warning;
                    }

                    string tempTargetName = target.Info.Name;

                    if (target.Info.Name.Length > maxNameLenght)
                    {
                        tempTargetName = target.Info.Name.Substring(0, maxNameLenght);
                    }

                    target.DistanceColor = Color.Gray;
                    if (target.Distance <= closeDistance)
                    {
                        target.DistanceColor = Color.LightGray;
                        if (target.Distance <= closeDistance - (closeDistance / 6)) target.DistanceColor = Color.White;
                        if (target.Distance <= closeDistance - (2 * closeDistance / 6)) target.DistanceColor = Color.LightYellow;
                        if (target.Distance <= closeDistance - (3 * closeDistance / 6)) target.DistanceColor = Color.Yellow;
                        if (target.Distance <= closeDistance - (4 * closeDistance / 6)) target.DistanceColor = Color.Orange;
                        if (target.Distance <= closeDistance - (5 * closeDistance / 6)) target.DistanceColor = Color.Red;
                    }

                    if (target.Info.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                    {                       
                        targetOutput.Add(ColorToColor(target.Color) + warning + " " + tempTargetName.ToString() +" "+ ColorToColor(target.DistanceColor) + Math.Round(target.Distance / 1000, 2).ToString() + "km", sorter);
                    }
                    else
                    {
                        string friendlyType = "";
                        if (target.Info.Name.ToString() == "")
                            friendlyType = "Suit";
                        friendOutput.Add(ColorToColor(target.Color) + tempTargetName.ToString() +" "+ ColorToColor(target.DistanceColor) + Math.Round(target.Distance / 1000, 2).ToString() + "km" + friendlyType, sorter);
                    }               
                }
                catch { }
            }
            WriteLcd(targetOutput, targetLCDs);
            WriteLcd(friendOutput, friendLCDs);
        }

        void ClearAll()
        {
            foreach (IMyTextSurface lcd in targetLCDs)
            {
                lcd.WriteText("");
            }
            foreach (IMyTextSurface lcd in friendLCDs)
            {
                lcd.WriteText("");
            }
            targetOutput.Clear();
            friendOutput.Clear();
        }

        void WriteLcd(Dictionary<String, double> outputDict, List<IMyTextSurface> lcdList)
        {
            IOrderedEnumerable<KeyValuePair<String, double>> sortedDict;

            if (sortByDistance)
                sortedDict = from entry in outputDict orderby entry.Value ascending select entry;
            else
                sortedDict = from entry in outputDict orderby entry.Value descending select entry;

            foreach (IMyTextSurface lcd in lcdList)
            {
                foreach (KeyValuePair<String, double> output in sortedDict)
                {
                    lcd.WriteText(output.Key + "\n", true);
                }
            }
        }
        string ColorToColor(Color color)
        {
            return $"<color={color.R},{color.G},{color.B},{color.A}>";
        }
        Color SuitColor(Color orig) => Color.Lerp(orig, Color.Yellow, 0.5f);
        void GetAllTargets()
        {
            approaching = false;
            targetDataDict.Clear();
            wcTargets.Clear();
            wcObstructions.Clear();
            wcapi.GetSortedThreats(Me, wcTargets);
            wcapi.GetObstructions(Me, wcObstructions);

            Dictionary<long, TargetData> temp = new Dictionary<long, TargetData>();

            foreach (var target in wcTargets)
            {
                AddTargetData(target.Key, target.Value);
            }
            foreach (var target in wcObstructions)
            {
                if (target.Name != "MyVoxelMap")
                {
                    AddTargetData(target, 0);
                }
            }

            var t = wcapi.GetAiFocus(Me.CubeGrid.EntityId, 0);
            if (t.HasValue && t.Value.EntityId != 0L)
                currentTarget = t.Value;

            foreach (var kvp in targetDataDict)
            {
                if (kvp.Key == Me.CubeGrid.EntityId)
                    continue;

                var targetData = kvp.Value;

                if (targetData.Info.EntityId != 0)
                    t = wcapi.GetAiFocus(targetData.Info.EntityId, 0);
                if (t.HasValue && t.Value.EntityId != 0)
                    targetData.Targeting = t.Value.EntityId;
                try
                {
                    targetData.Distance = Vector3D.Distance(targetData.Info.Position, cockpits[0].CenterOfMass);
                }
                catch
                {
                    targetData.Distance = Vector3D.Distance(targetData.Info.Position, Me.CubeGrid.GetPosition());
                }
                if (prevDistances.ContainsKey(targetData.Info.EntityId))
                    targetData.ApproachSpeed = (prevDistances[targetData.Info.EntityId] - targetData.Distance) / tickSpeed;

                targetData.Color = Color.White;

                if (!currentTarget.IsEmpty() && kvp.Key == currentTarget.EntityId)
                {
                    targetData.MyTarget = true;
                }

                switch (targetData.Info.Relationship)
                {
                    case MyRelationsBetweenPlayerAndBlock.Enemies:
                        targetData.Color = enemyColor;
                        if (approachWarning && targetData.Distance < approachDistance && targetData.MyTarget == false && targetData.Info.Type != MyDetectedEntityType.CharacterHuman)
                        {
                            if (targetData.ApproachSpeed > approachSpeed || targetData.Distance < (approachDistance / 2))
                            {
                                approaching = true;
                                if (!soundPlayed)
                                {
                                    SoundWarning(true);
                                    soundPlayed = true;
                                }
                            }
                        }
                        if (Me.CubeGrid.EntityId == targetData.Targeting)
                        {
                            targetData.Color = targetingColor;
                        }
                        break;

                    case MyRelationsBetweenPlayerAndBlock.Owner:
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    case MyRelationsBetweenPlayerAndBlock.Friends:
                        targetData.Color = friendColor;
                        break;
                    case MyRelationsBetweenPlayerAndBlock.Neutral:
                        targetData.Color = neutralColor;
                        break;

                    default:
                        targetData.Threat = -1;
                        if (targetData.Info.Type == MyDetectedEntityType.Unknown)
                        {
                            targetData.Color = friendColor;
                        }
                        break;
                }

                if (targetData.Info.Type == MyDetectedEntityType.CharacterHuman)
                {
                    targetData.Color = SuitColor(targetData.Color);
                }

                temp[targetData.Info.EntityId] = targetData;
            }

            targetDataDict.Clear();
            foreach (var item in temp)
                targetDataDict[item.Key] = item.Value;
            foreach (var item in targetDataDict)
                prevDistances[item.Key] = item.Value.Distance;
            if (!approaching)
            {
                SoundWarning(false);
                soundPlayed = false;
            }
        }
        void SoundWarning(bool onOff)
        {
            if (soundblocks.Count > 0 && soundblocks[0] != null && soundblocks[0].IsWorking && soundblocks[0].IsFunctional)
            {
                if (onOff)
                {
                    soundblocks[0].Enabled = true;
                    soundblocks[0].SelectedSound = warningSound;
                    soundblocks[0].LoopPeriod = 1800;
                    soundblocks[0].Range = 500;
                    soundblocks[0].Volume = 100;
                    soundblocks[0].Play();
                }
                else
                {
                    soundblocks[0].Stop();
                }
            }
        }
        void AddTargetData(MyDetectedEntityInfo targetInfo, float threat = 0f)
        {
            if (!targetDataDict.ContainsKey(targetInfo.EntityId))
            {
                TargetData targetData = new TargetData(targetInfo);
                targetData.Threat = threat;
                targetDataDict[targetInfo.EntityId] = targetData;
            }
        }

        List<IMyCockpit> cockpits = new List<IMyCockpit>();
        List<IMySoundBlock> soundblocks = new List<IMySoundBlock>();

        void GetBlocks()
        {
            allLCDs.Clear();
            friendLCDs.Clear();
            targetLCDs.Clear();
            cockpits.Clear();
            GridTerminalSystem.GetBlocksOfType(allLCDs);
            GridTerminalSystem.GetBlocksOfType(cockpits);
            GridTerminalSystem.GetBlocksOfType(soundblocks);
            foreach (IMyTextPanel lcd in allLCDs)
            {
                if (lcd.CustomName.Contains("Friend") && lcd.IsSameConstructAs(Me))
                {
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.BackgroundColor = Color.Black;
                    friendLCDs.Add(lcd);
                }
                else if (lcd.CustomName.Contains("Target") && lcd.IsSameConstructAs(Me))
                {
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.BackgroundColor = Color.Black;
                    targetLCDs.Add(lcd);
                }
            }
        }
        public class WcPbApi
        {
            private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
            private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
            private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> _getObstructions;
            private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Func<IMyTerminalBlock, long, int, bool> _setAiFocus;
            private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
            private Action<IMyTerminalBlock, long, int> _setWeaponTarget;
            private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
            private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
            private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
            private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
            private Func<IMyTerminalBlock, long, int, bool> _isTargetAligned;
            private Func<IMyTerminalBlock, long, int, MyTuple<bool, VRageMath.Vector3D?>> _isTargetAlignedExtended;
            private Func<IMyTerminalBlock, long, int, bool> _canShootTarget;
            private Func<IMyTerminalBlock, long, int, VRageMath.Vector3D?> _getPredictedTargetPos;
            private Func<long, bool> _hasGridAi;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
            private Func<long, float> _getOptimalDps;
            private Func<IMyTerminalBlock, int, string> _getActiveAmmo;
            private Action<IMyTerminalBlock, int, string> _setActiveAmmo;
            private Func<long, float> _getConstructEffectiveDps;
            private Func<IMyTerminalBlock, long> _getPlayerController;
            private Func<IMyTerminalBlock, long, bool, bool, bool> _isTargetValid;
            private Func<IMyTerminalBlock, int, MyTuple<VRageMath.Vector3D, VRageMath.Vector3D>> _getWeaponScope;
            private Func<IMyTerminalBlock, MyTuple<bool, bool>> _isInRange;

            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception("WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;

                AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
                AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
                AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "GetObstructions", ref _getObstructions);
                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "SetAiFocus", ref _setAiFocus);
                AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
                AssignMethod(delegates, "SetWeaponTarget", ref _setWeaponTarget);
                AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
                AssignMethod(delegates, "ToggleWeaponFire", ref _toggleWeaponFire);
                AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
                AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
                AssignMethod(delegates, "IsTargetAligned", ref _isTargetAligned);
                AssignMethod(delegates, "IsTargetAlignedExtended", ref _isTargetAlignedExtended);
                AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
                AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                AssignMethod(delegates, "GetOptimalDps", ref _getOptimalDps);
                AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
                AssignMethod(delegates, "SetActiveAmmo", ref _setActiveAmmo);
                AssignMethod(delegates, "GetConstructEffectiveDps", ref _getConstructEffectiveDps);
                AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
                AssignMethod(delegates, "IsTargetValid", ref _isTargetValid);
                AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);
                AssignMethod(delegates, "IsInRange", ref _isInRange);
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
                    throw new Exception($"{GetType().Name} Couldnt find {name} delegate of type {typeof(T)}");

                field = del as T;
                if (field == null)
                    throw new Exception(
                        $"{GetType().Name} Delegate {name} is not type {typeof(T)} instead its {del.GetType()}");
            }

            public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);

            public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
                _getCoreStaticLaunchers?.Invoke(collection);

            public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);

            public void GetSortedThreats(IMyTerminalBlock pBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pBlock, collection);
            public void GetObstructions(IMyTerminalBlock pBlock, ICollection<MyDetectedEntityInfo> collection) =>
                _getObstructions?.Invoke(pBlock, collection);
            public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

            public bool SetAiFocus(IMyTerminalBlock pBlock, long target, int priority = 0) =>
                _setAiFocus?.Invoke(pBlock, target, priority) ?? false;

            public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
                _getWeaponTarget?.Invoke(weapon, weaponId);

            public void SetWeaponTarget(IMyTerminalBlock weapon, long target, int weaponId = 0) =>
                _setWeaponTarget?.Invoke(weapon, target, weaponId);

            public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
                _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);

            public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
                _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);

            public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
                bool shootReady = false) =>
                _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

            public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
                _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

            public bool IsTargetAligned(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;

            public MyTuple<bool, VRageMath.Vector3D?> IsTargetAlignedExtended(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _isTargetAlignedExtended?.Invoke(weapon, targetEnt, weaponId) ?? new MyTuple<bool, VRageMath.Vector3D?>();

            public bool CanShootTarget(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

            public VRageMath.Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
            public float GetOptimalDps(long entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

            public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
                _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

            public void SetActiveAmmo(IMyTerminalBlock weapon, int weaponId, string ammoType) =>
                _setActiveAmmo?.Invoke(weapon, weaponId, ammoType);
            public float GetConstructEffectiveDps(long entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;

            public long GetPlayerController(IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;
            public bool IsTargetValid(IMyTerminalBlock weapon, long targetId, bool onlyThreats, bool checkRelations) =>
                _isTargetValid?.Invoke(weapon, targetId, onlyThreats, checkRelations) ?? false;

            public MyTuple<VRageMath.Vector3D, VRageMath.Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<VRageMath.Vector3D, VRageMath.Vector3D>();
            public MyTuple<bool, bool> IsInRange(IMyTerminalBlock block) =>
                _isInRange?.Invoke(block) ?? new MyTuple<bool, bool>();
        }
