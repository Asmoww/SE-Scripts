        double maxMs = 0.3;
        bool runtime = true;
        int statusPriority = 3;

        string scriptNameVersion = "Template 1.0";
        public static WcPbApi wcapi = new WcPbApi();
        int tickNum = 0;
        string scriptRunningChar = @" [ ";
        double averageRuntime = 0;

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
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "")
            {
                switch (argument)
                {

                }
            }
            tickNum++;
            averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs / 10 * 0.01);
            if (averageRuntime > maxMs * 0.9)
            {
                return;
            }
            Echo(Math.Round(averageRuntime, 4).ToString() + "ms");
            if (scriptRunningChar == @" [ ") scriptRunningChar = @" ) ";
            else scriptRunningChar = @" [ ";
            if (runtime) SendStatus("<color=100,100,100,255>"+ scriptNameVersion +" <color=70,70,70,255>" + Math.Round(averageRuntime, 2).ToString() + scriptRunningChar + maxMs.ToString() + " ms");
            WriteStatus();
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
     
        string ColorToColor(Color color)
        {
            return $"<color={color.R},{color.G},{color.B},{color.A}>";
        }      

        void GetBlocks()
        {
            statusLCDs.Clear();
            progBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, SortBlocks);
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
            return false;
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
