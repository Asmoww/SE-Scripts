//Lists nearby grids to LCDs, takes info from weaponcore api
//meant to work with hudlcd plugin
//name LCDs "Friend LCD" and "Target LCD"
double maxMs = 0.4;

public static WcPbApi wcapi = new WcPbApi();
Dictionary<MyDetectedEntityInfo, float> wcTargets = new Dictionary<MyDetectedEntityInfo, float>();
List<MyDetectedEntityInfo> wcObstructions = new List<MyDetectedEntityInfo>();
Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
static MyDetectedEntityInfo currentTarget;
int tickNum = 0;
double averageRuntime = 0;

struct TargetData
{
    public MyDetectedEntityInfo Info;
    public long Targeting;
    public bool MyTarget;
    public double Distance;
    public float Threat;
    public Color Color;

    public TargetData(MyDetectedEntityInfo info, long targeting = 0, bool myTarget = false, double distance = 0, float threat = 0, Color color = default(Color))
    {
        Info = info;
        Targeting = targeting;
        MyTarget = myTarget;
        Distance = distance;
        Threat = threat;
        Color = color;
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
    averageRuntime = averageRuntime * 0.99 + (Runtime.LastRunTimeMs/10 * 0.01);
    Echo(Math.Round(averageRuntime, 4).ToString() + "ms");
    Echo("LCDs: "+(friendLCDs.Count + targetLCDs.Count).ToString());
    if (averageRuntime > maxMs * 0.9)
    {
        return;
    }
    if (targetLCDs != null && friendLCDs != null)
    {
        GetAllTargets();
        TargetLCD();
    }
    if (tickNum == 10)
    {
        GetBlocks();
        tickNum = 0;
    }
}

public struct Output
{
    public string Text;
    public Color Color;

    public Output(string text, Color color)
    {
        Text = text;
        Color = color;
    }
}

Dictionary<Output, double> targetOutput = new Dictionary<Output, double>();
Dictionary<Output, double> friendOutput = new Dictionary<Output, double>();

static List<IMyTextPanel> allLCDs = new List<IMyTextPanel>();

static List<IMyTextSurface> targetLCDs = new List<IMyTextSurface>();

static List<IMyTextSurface> friendLCDs = new List<IMyTextSurface>();

void TargetLCD()
{

    ClearLcd();

    targetOutput.Clear();
    friendOutput.Clear();

    foreach (var obj in targetDataDict)
    {
        try
        {
            var target = obj.Value;
            string type;
            switch (target.Info.Type)
            {
                case MyDetectedEntityType.CharacterHuman:
                    type = "Suit";
                    break;
                case MyDetectedEntityType.LargeGrid:
                    type = "L";
                    break;
                case MyDetectedEntityType.SmallGrid:
                    type = "S";
                    break;
                default:
                    type = "";
                    break;
            }
            if (target.MyTarget)
            {
                targetOutput.Add(new Output("@ "+type + " " + Math.Round(target.Distance / 1000, 2).ToString() + "km " + target.Info.Name.ToString(), target.Color), 0);
            }
            else
            {
                if (target.Info.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                {
                    targetOutput.Add(new Output(type + " " + Math.Round(target.Distance / 1000, 2).ToString() + "km " + target.Info.Name.ToString(), target.Color), target.Distance);
                }
                else
                {
                    if (target.Info.Type != MyDetectedEntityType.CharacterHuman)
                    {
                        friendOutput.Add(new Output(type + " " + Math.Round(target.Distance / 1000, 2).ToString() + "km " + target.Info.Name.ToString(), target.Color), target.Distance);
                    }
                }
            }
        }
        catch { }
    }
    WriteLcd(targetOutput, targetLCDs);
    WriteLcd(friendOutput, friendLCDs);
}

void ClearLcd()
{
    foreach (IMyTextSurface lcd in targetLCDs)
    {
        lcd.WriteText("");
    }
    foreach (IMyTextSurface lcd in friendLCDs)
    {
        lcd.WriteText("");
    }
}

void WriteLcd(Dictionary<Output, double> outputDict, List<IMyTextSurface> lcdList)
{
    var sortedDict = from entry in outputDict orderby entry.Value ascending select entry;
    
    foreach (IMyTextSurface lcd in lcdList)
    {
        foreach (KeyValuePair<Output, double> output in sortedDict)
        {
            if (output.Key.Text.Length > 30)
            {
                lcd.WriteText(ColorToColor(output.Key.Color) + output.Key.Text.Substring(0, 30) + "...\n", true);
            }
            else
            {
                lcd.WriteText(ColorToColor(output.Key.Color)+output.Key.Text + "\n", true);
            }
        }
    }
}

string ColorToColor(Color color)
{
    return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()},{color.A.ToString()}>";
}
Color SuitColor(Color orig) => Color.Lerp(orig, Color.Yellow, 0.5f);
void GetAllTargets()
{
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
        targetData.Distance = Vector3D.Distance(targetData.Info.Position, Me.CubeGrid.GetPosition());

        targetData.Color = Color.White;
        switch (targetData.Info.Relationship)
        {
            case MyRelationsBetweenPlayerAndBlock.Enemies:
                targetData.Color = Color.Red;
                if (Me.CubeGrid.EntityId == targetData.Targeting)
                {
                    targetData.Color = Color.Orange;
                }
                break;

            case MyRelationsBetweenPlayerAndBlock.Owner:
            case MyRelationsBetweenPlayerAndBlock.FactionShare:
            case MyRelationsBetweenPlayerAndBlock.Friends:                       
                targetData.Color = Color.Green;
                break;
            case MyRelationsBetweenPlayerAndBlock.Neutral:
                targetData.Color = Color.LightBlue;
                break;

            default:
                targetData.Threat = -1;
                if (targetData.Info.Type == MyDetectedEntityType.Unknown)
                {
                    targetData.Color = Color.Lime;
                }
                break;
        }

        if (targetData.Info.Type == MyDetectedEntityType.CharacterHuman)
        {
            targetData.Color = SuitColor(targetData.Color);
        }

        if (!currentTarget.IsEmpty() && kvp.Key == currentTarget.EntityId)
        {
            targetData.MyTarget = true;
        }
      
        temp[targetData.Info.EntityId] = targetData;
    }

    targetDataDict.Clear();
    foreach (var item in temp)
        targetDataDict[item.Key] = item.Value;
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
void GetBlocks()
{
    friendLCDs.Clear();
    targetLCDs.Clear();
    GridTerminalSystem.GetBlocksOfType(allLCDs);
    foreach (IMyTextPanel lcd in allLCDs)
    {
        if (lcd.CustomName == "Friend LCD")
        {
            lcd.ContentType = ContentType.TEXT_AND_IMAGE;
            lcd.BackgroundColor = Color.Black;
            friendLCDs.Add(lcd);
        }
        else if(lcd.CustomName == "Target LCD")
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
