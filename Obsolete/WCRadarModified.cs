        /*
 * Based off of whips radar for the lcd stuff, modified to work with weapon core by dude.
 * 
 * version 1.5
 * 
 * *NEW* Split up the target lcd to separate enemies from friendlies, add a "Friend LCD" to see non-hostiles.
 * 	  LCD output will now overflow onto another LCD if there are more lines than will fit on a panel.
 * 	  You can specify the order of the panels by adding a number to the end of the LCDs name starting with '1'.
 * 
 * To see the radar screen, name an LCD 'Radar'
 * To see a list of nearby enemies, name an LCD 'Target LCD'
 * To see a list of nearby friendlies/neutrals, name an LCD 'Friend LCD'
 * To have lights and/or sound blocks trigger when an enemy is around, add 'alert' to their name.
 * Will show targets around you up to your current max weapon range. 
 * Ships that are targeting you will be colored yellow.
 * To adjust the visible range (zoom) of the radar display, use the 'range ##' (in meters) argument. Default is 20km. (Example argument: range 10000 )
 */
        //Arthasnack edit : changed Update1 to Update10 in the code to allow UD compatibility
        //Asmoww edit: changed target and friendly lcds: script -> text

        string textPanelName = "Radar";
        string referenceName = "Cockpit";

        const string INI_SECTION_GENERAL = "General";
        const string INI_SHOW_ASTEROIDS = "Show asteroids";
        const string INI_RADAR_NAME = "Text surface name tag";
        const string INI_REF_NAME = "Optional reference block name";
        const string INI_USE_RANGE_OVERRIDE = "Use range override";
        const string INI_RANGE_OVERRIDE = "Range override (m)";
        const string INI_PROJ_ANGLE = "Projection angle in degrees (0 is flat)";
        const string INI_DRAW_QUADRANTS = "Draw quadrants";

        const string INI_SECTION_COLORS = "Colors";
        const string INI_TITLE_BAR = "Title bar";
        const string INI_TEXT = "Text";
        const string INI_BACKGROUND = "Background";
        const string INI_RADAR_LINES = "Lines";
        const string INI_PLANE = "Plane";
        const string INI_ENEMY = "Enemy icon";
        const string INI_ENEMY_ELEVATION = "Enemy elevation";
        const string INI_NEUTRAL = "Neutral icon";
        const string INI_NEUTRAL_ELEVATION = "Neutral elevation";
        const string INI_FRIENDLY = "Friendly icon";
        const string INI_FRIENDLY_ELEVATION = "Friendly elevation";

        const string INI_SECTION_TEXT_SURF_PROVIDER = "Text Surface Config";
        const string INI_TEXT_SURFACE_TEMPLATE = "Show on screen";

        float rangeOverride = 7000;
        bool useRangeOverride = true;
        bool showAsteroids = false;
        bool drawQuadrants = false;

        Color titleBarColor = new Color(50, 50, 50, 5);
        Color backColor = new Color(0, 0, 0, 255);
        Color lineColor = new Color(60, 60, 60, 10);
        Color planeColor = new Color(50, 50, 50, 5);
        Color enemyIconColor = new Color(150, 0, 0, 255);
        Color enemyElevationColor = new Color(75, 0, 0, 100);
        Color neutralIconColor = new Color(150, 150, 150, 255);
        Color neutralElevationColor = new Color(75, 75, 75, 100);
        Color allyIconColor = new Color(0, 150, 0, 255);
        Color allyElevationColor = new Color(0, 75, 0, 100);
        Color textColor = new Color(100, 100, 100, 100);
        Color missileLockColor = new Color(0, 100, 100, 255);

        float MaxRange
        {
            get
            {
                return rangeOverride;
            }
        }

        List<IMyShipController> Controllers
        {
            get
            {
                return allControllers;
            }
        }

        float projectionAngle = 50f;

        Scheduler scheduler;
        RuntimeTracker runtimeTracker;
        ScheduledAction grabBlockAction;

        Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
        List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();
        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<IMyTextSurface> textSurfaces = new List<IMyTextSurface>();
        List<IMyShipController> taggedControllers = new List<IMyShipController>();
        List<IMyShipController> allControllers = new List<IMyShipController>();
        List<IMySoundBlock> soundBlocks = new List<IMySoundBlock>();
        List<IMyLightingBlock> warningLights = new List<IMyLightingBlock>();
        HashSet<long> myGridIds = new HashSet<long>();
        IMyTerminalBlock reference;
        IMyShipController lastActiveShipController = null;

        const double cycleTime = 1.0 / 60.0;
        string lastSetupResult = "";
        bool isSetup = false;
        bool _clearSpriteCache = false;
        readonly RadarSurface radarSurface;
        readonly MyIni generalIni = new MyIni();
        readonly MyIni textSurfaceIni = new MyIni();
        readonly MyCommandLine _commandLine = new MyCommandLine();
        WcPbApi wcapi;
        bool wcapiActive = false;
        Dictionary<MyDetectedEntityInfo, float> wcTargets = new Dictionary<MyDetectedEntityInfo, float>();
        List<MyDetectedEntityInfo> wcObstructions = new List<MyDetectedEntityInfo>();
        static MyDetectedEntityInfo currentTarget;

        Program()
        {
            ParseCustomDataIni();
            GrabBlocks();
            radarSurface = new RadarSurface(titleBarColor, backColor, lineColor, planeColor, textColor, missileLockColor, projectionAngle, MaxRange, drawQuadrants);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            runtimeTracker = new RuntimeTracker(this);

            scheduler = new Scheduler(this);
            grabBlockAction = new ScheduledAction(GrabBlocks, 0.1);
            scheduler.AddScheduledAction(grabBlockAction);
            scheduler.AddScheduledAction(UpdateRadarRange, 1);
            scheduler.AddScheduledAction(PrintDetailedInfo, 1);

            scheduler.AddScheduledAction(TargetLCD, 2);

            scheduler.AddQueuedAction(GetAllTargets, cycleTime);
            scheduler.AddQueuedAction(radarSurface.SortContacts, cycleTime);

            float step = 1f / 8f;
            scheduler.AddQueuedAction(() => Draw(0 * step, 1 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(1 * step, 2 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(2 * step, 3 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(3 * step, 4 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(4 * step, 5 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(5 * step, 6 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(6 * step, 7 * step), cycleTime);
            scheduler.AddQueuedAction(() => Draw(7 * step, 8 * step), cycleTime);


            wcapi = new WcPbApi();
        }

        void Main(string arg, UpdateType updateSource)
        {
            runtimeTracker.AddRuntime();

            if (_commandLine.TryParse(arg))
                HandleArguments();

            scheduler.Update();


            runtimeTracker.AddInstructions();
        }

        void HandleArguments()
        {
            int argCount = _commandLine.ArgumentCount;

            if (argCount == 0)
                return;

            if (_commandLine.Argument(0).ToLowerInvariant() == "range")
            {
                if (argCount != 2)
                {
                    return;
                }

                float range = 0;
                if (float.TryParse(_commandLine.Argument(1), out range))
                {
                    useRangeOverride = true;
                    rangeOverride = range;

                    UpdateRadarRange();

                    generalIni.Clear();
                    generalIni.TryParse(Me.CustomData);
                    generalIni.Set(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE, rangeOverride);
                    generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
                    Me.CustomData = generalIni.ToString();
                }
                else if (string.Equals(_commandLine.Argument(1), "default"))
                {
                    useRangeOverride = false;

                    UpdateRadarRange();

                    generalIni.Clear();
                    generalIni.TryParse(Me.CustomData);
                    generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
                    Me.CustomData = generalIni.ToString();
                }
                return;
            }
        }

        void Draw(float startProportion, float endProportion)
        {
            int start = (int)(startProportion * textSurfaces.Count);
            int end = (int)(endProportion * textSurfaces.Count);

            for (int i = start; i < end; ++i)
            {
                var textSurface = textSurfaces[i];
                radarSurface.DrawRadar(textSurface, _clearSpriteCache);
            }
        }

        void PrintDetailedInfo()
        {
            Echo($"Range: {MaxRange} m");
            Echo($"Text surfaces: {textSurfaces.Count}");
            Echo($"Reference: {reference?.CustomName}");
            Echo($"{lastSetupResult}");
            Echo($"Next refresh in {Math.Max(grabBlockAction.RunInterval - grabBlockAction.TimeSinceLastRun, 0):N0} seconds");
            Echo(runtimeTracker.Write());
        }

        void UpdateRadarRange()
        {
            radarSurface.Range = MaxRange;
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

        bool enemyNearby;
        readonly Color noneIconColor = new Color(50, 50, 50, 128);
        readonly Color noneElevationColor = new Color(15, 15, 15, 64);
        Color SuitColor(Color orig) => Color.Lerp(orig, Color.Yellow, 0.5f);
        void GetAllTargets()
        {
            if (!isSetup)
                return;

            if (!wcapiActive)
            {
                wcapiActive = wcapi.Activate(Me);
            }

            enemyNearby = false;
            targetDataDict.Clear();
            radarSurface.ClearContacts();
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
                AddTargetData(target, 0);
            }

            var t = wcapi.GetAiFocus(Me.CubeGrid.EntityId, 0);
            if (t.HasValue && t.Value.EntityId != 0L)
                currentTarget = t.Value;

            reference = GetControlledShipController(Controllers);
            if (reference == null)
            {
                if (lastActiveShipController != null)
                {
                    reference = lastActiveShipController;
                }
                else if (reference == null && Controllers.Count != 0)
                {
                    reference = Controllers[0];
                }
                else
                {
                    reference = Me;
                }
            }

            if (reference is IMyShipController)
                lastActiveShipController = (IMyShipController)reference;

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

                Color targetIconColor = neutralIconColor;
                Color targetElevationColor = neutralElevationColor;
                targetData.Color = Color.White;
                RadarSurface.Relation relation = RadarSurface.Relation.Neutral;
                switch (targetData.Info.Relationship)
                {
                    case MyRelationsBetweenPlayerAndBlock.Enemies:
                        enemyNearby = true;
                        targetIconColor = enemyIconColor;
                        targetElevationColor = enemyElevationColor;
                        targetData.Color = Color.Red;
                        relation = RadarSurface.Relation.Hostile;
                        if (Me.CubeGrid.EntityId == targetData.Targeting)
                        {
                            targetIconColor = Color.Yellow;
                            targetElevationColor = Color.Yellow;
                            targetData.Color = Color.Yellow;
                        }
                        break;

                    case MyRelationsBetweenPlayerAndBlock.Owner:
                    case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    case MyRelationsBetweenPlayerAndBlock.Friends:
                        targetIconColor = allyIconColor;
                        targetElevationColor = allyElevationColor;
                        targetData.Color = Color.Lime;
                        relation = RadarSurface.Relation.Allied;
                        break;

                    default:
                        targetData.Threat = -1;
                        if (targetData.Info.Name == "MyVoxelMap")
                        {
                            targetIconColor = noneIconColor;
                            targetElevationColor = noneElevationColor;
                            targetData.Color = noneIconColor;
                            relation = RadarSurface.Relation.None;
                            break;
                        }
                        if (targetData.Info.Type == MyDetectedEntityType.Unknown)
                        {
                            targetIconColor = allyIconColor;
                            targetElevationColor = allyElevationColor;
                            targetData.Color = Color.Lime;
                            relation = RadarSurface.Relation.Allied;
                            break;
                        }
                        break;
                }

                if (targetData.Info.Type == MyDetectedEntityType.CharacterHuman)
                {
                    targetIconColor = SuitColor(targetIconColor);
                    targetElevationColor = SuitColor(targetElevationColor);
                    targetData.Color = SuitColor(targetData.Color);
                }

                if (!currentTarget.IsEmpty() && kvp.Key == currentTarget.EntityId)
                {
                    targetData.MyTarget = true;
                }

                radarSurface.AddContact(targetData.Info.Position, reference.WorldMatrix, targetData.Info.Type, targetIconColor, targetElevationColor, targetData.Info.Name, relation, targetData.MyTarget, targetData.Distance, targetData.Info.Velocity, targetData.Threat);
                temp[targetData.Info.EntityId] = targetData;
            }

            targetDataDict.Clear();
            foreach (var item in temp)
                targetDataDict[item.Key] = item.Value;
        }

        Dictionary<Output, double> targetOutput = new Dictionary<Output, double>();
        Dictionary<Output, double> friendOutput = new Dictionary<Output, double>();

        void TargetLCD()
        {           
            if (targetLCDs == null && friendLCDs == null)
                return;

            ClearLcd();

            targetOutput.Clear();
            friendOutput.Clear();


            foreach (var obj in targetDataDict)
            {
                try
                {
                    var target = obj.Value;
                    if (target.Info.Name.ToString() == "MyVoxelMap")
                    {
                        continue;
                    }
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
                        targetOutput.Add(new Output(type + " " + Math.Round(target.Distance / 1000, 2).ToString() + "km " + target.Info.Name.ToString(), target.Color), 0);
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

        class RadarSurface
        {
            float _range = 0f;
            public float Range
            {
                get
                {
                    return _range;
                }
                set
                {
                    if (value == _range)
                        return;
                    _range = value;
                }
            }
            public enum Relation { None = 0, Allied = 1, Neutral = 2, Hostile = 3 }
            public readonly StringBuilder Debug = new StringBuilder();

            string FONT = "Debug";
            const float TITLE_TEXT_SIZE = 1.2f;
            const float HUD_TEXT_SIZE = 0.9f;
            const float RANGE_TEXT_SIZE = 1.0f;
            const float TGT_ELEVATION_LINE_WIDTH = 2f;
            const float QUADRANT_LINE_WIDTH = 2f;
            const float TITLE_BAR_HEIGHT = 40;

            Color _titleBarColor;
            Color _backColor;
            Color _lineColor;
            Color _quadrantLineColor;
            Color _planeColor;
            Color _textColor;
            Color _targetLockColor;
            float _projectionAngleDeg;
            float _radarProjectionCos;
            float _radarProjectionSin;
            bool _drawQuadrants;
            int _allyCount = 0;
            int _hostileCount = 0;
            Vector2 _quadrantLineDirection;

            readonly Vector2 DROP_SHADOW_OFFSET = new Vector2(2, 2);
            readonly Vector2 TGT_ICON_SIZE = new Vector2(10, 10);
            readonly Vector2 SHIP_ICON_SIZE = new Vector2(8, 4);
            readonly List<TargetInfo> _targetList = new List<TargetInfo>();
            readonly List<TargetInfo> _targetsBelowPlane = new List<TargetInfo>();
            readonly List<TargetInfo> _targetsAbovePlane = new List<TargetInfo>();
            readonly Dictionary<Relation, string> _spriteMap = new Dictionary<Relation, string>()
{
{ Relation.None, "None" },
{ Relation.Allied, "SquareSimple" },
{ Relation.Neutral, "Triangle" },
{ Relation.Hostile, "Circle" },
};

            struct TargetInfo
            {
                public Vector3 Position;
                public Color IconColor;
                public Color ElevationColor;
                public string Icon;
                public bool TargetLock;
                public double Distance;
                public Vector3D Velocity;
                public int ThreatScore;
                public MyDetectedEntityType Type;
                public string Name;
                public bool TargetingMe;
            }

            public RadarSurface(Color titleBarColor, Color backColor, Color lineColor, Color planeColor, Color textColor, Color targetLockColor, float projectionAngleDeg, float range, bool drawQuadrants)
            {
                UpdateFields(titleBarColor, backColor, lineColor, planeColor, textColor, targetLockColor, projectionAngleDeg, range, drawQuadrants);
            }

            public void UpdateFields(Color titleBarColor, Color backColor, Color lineColor, Color planeColor, Color textColor, Color targetLockColor, float projectionAngleDeg, float range, bool drawQuadrants)
            {
                _titleBarColor = titleBarColor;
                _backColor = backColor;
                _lineColor = lineColor;
                _quadrantLineColor = new Color((byte)(lineColor.R / 2), (byte)(lineColor.G / 2), (byte)(lineColor.B / 2), (byte)(lineColor.A / 2));
                _planeColor = planeColor;
                _textColor = textColor;
                _projectionAngleDeg = projectionAngleDeg;
                _drawQuadrants = drawQuadrants;
                _targetLockColor = targetLockColor;
                Range = range;


                var rads = MathHelper.ToRadians(_projectionAngleDeg);
                _radarProjectionCos = (float)Math.Cos(rads);
                _radarProjectionSin = (float)Math.Sin(rads);

                _quadrantLineDirection = new Vector2(0.25f * MathHelper.Sqrt2, 0.25f * MathHelper.Sqrt2 * _radarProjectionCos);
            }

            public void AddContact(Vector3D position, MatrixD worldMatrix, MyDetectedEntityType type, Color iconColor, Color elevationLineColor, string name, Relation relation, bool targetLock, double distance = 0, Vector3D velocity = new Vector3D(), float threat = 0)
            {
                int threatScore = 0;

                if (threat > 0)
                    threatScore = 1;
                if (threat >= 0.0625)
                    threatScore = 2;
                if (threat >= 0.125)
                    threatScore = 3;
                if (threat >= 0.25)
                    threatScore = 4;
                if (threat >= 0.5)
                    threatScore = 5;
                if (threat >= 1)
                    threatScore = 6;
                if (threat >= 2)
                    threatScore = 7;
                if (threat >= 3)
                    threatScore = 8;
                if (threat >= 4)
                    threatScore = 9;
                if (threat >= 5)
                    threatScore = 10;

                var transformedDirection = Vector3D.TransformNormal(position - worldMatrix.Translation, Matrix.Transpose(worldMatrix));
                float xOffset = (float)(transformedDirection.X / Range);
                float yOffset = (float)(transformedDirection.Z / Range);
                float zOffset = (float)(transformedDirection.Y / Range);

                string spriteName = "";
                _spriteMap.TryGetValue(relation, out spriteName);

                var targetInfo = new TargetInfo()
                {
                    Position = new Vector3(xOffset, yOffset, zOffset),
                    ElevationColor = elevationLineColor,
                    IconColor = iconColor,
                    Icon = spriteName,
                    TargetLock = targetLock,
                    Distance = distance,
                    Velocity = velocity,
                    ThreatScore = threatScore,
                    Type = type,
                    Name = name
                };

                switch (relation)
                {
                    case Relation.Allied:
                        ++_allyCount;
                        break;

                    case Relation.Hostile:
                        ++_hostileCount;
                        break;
                }

                _targetList.Add(targetInfo);
            }

            public void SortContacts()
            {
                _targetsBelowPlane.Clear();
                _targetsAbovePlane.Clear();

                _targetList.Sort((a, b) => (a.Position.Y).CompareTo(b.Position.Y));

                foreach (var target in _targetList)
                {
                    if (target.Position.Z >= 0)
                        _targetsAbovePlane.Add(target);
                    else
                        _targetsBelowPlane.Add(target);
                }
            }

            public void ClearContacts()
            {
                _targetList.Clear();
                _targetsAbovePlane.Clear();
                _targetsBelowPlane.Clear();
                _allyCount = 0;
                _hostileCount = 0;
            }

            static void DrawBoxCorners(MySpriteDrawFrame frame, Vector2 boxSize, Vector2 centerPos, float lineLength, float lineWidth, Color color)
            {
                Vector2 horizontalSize = new Vector2(lineLength, lineWidth);
                Vector2 verticalSize = new Vector2(lineWidth, lineLength);

                Vector2 horizontalOffset = 0.5f * horizontalSize;
                Vector2 verticalOffset = 0.5f * verticalSize;

                Vector2 boxHalfSize = 0.5f * boxSize;
                Vector2 boxTopLeft = centerPos - boxHalfSize;
                Vector2 boxBottomRight = centerPos + boxHalfSize;
                Vector2 boxTopRight = centerPos + new Vector2(boxHalfSize.X, -boxHalfSize.Y);
                Vector2 boxBottomLeft = centerPos + new Vector2(-boxHalfSize.X, boxHalfSize.Y);

                MySprite sprite;

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxTopLeft + horizontalOffset, rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxTopLeft + verticalOffset, rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxTopRight + new Vector2(-horizontalOffset.X, horizontalOffset.Y), rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxTopRight + new Vector2(-verticalOffset.X, verticalOffset.Y), rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxBottomLeft + new Vector2(horizontalOffset.X, -horizontalOffset.Y), rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxBottomLeft + new Vector2(verticalOffset.X, -verticalOffset.Y), rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxBottomRight - horizontalOffset, rotation: 0, color: color);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxBottomRight - verticalOffset, rotation: 0, color: color);
                frame.Add(sprite);
            }

            public void DrawRadar(IMyTextSurface surface, bool clearSpriteCache)
            {
                surface.ContentType = ContentType.SCRIPT;
                surface.Script = "";

                Vector2 surfaceSize = surface.TextureSize;
                Vector2 screenCenter = surfaceSize * 0.5f;
                Vector2 viewportSize = surface.SurfaceSize;
                Vector2 scale = viewportSize / 512f;
                float minScale = Math.Min(scale.X, scale.Y);
                float sideLength = Math.Min(viewportSize.X, viewportSize.Y - TITLE_BAR_HEIGHT * minScale);

                Vector2 radarCenterPos = screenCenter;
                Vector2 radarPlaneSize = new Vector2(sideLength, sideLength * _radarProjectionCos);

                using (var frame = surface.DrawFrame())
                {
                    if (clearSpriteCache)
                    {
                        frame.Add(new MySprite());
                    }

                    MySprite sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _backColor);
                    sprite.Position = screenCenter;
                    frame.Add(sprite);

                    DrawRadarPlaneBackground(frame, radarCenterPos, radarPlaneSize);

                    foreach (var targetInfo in _targetsBelowPlane)
                    {
                        DrawTargetIcon(frame, radarCenterPos, radarPlaneSize, targetInfo, minScale);
                    }

                    DrawRadarPlane(frame, viewportSize, screenCenter, radarCenterPos, radarPlaneSize, minScale);

                    foreach (var targetInfo in _targetsAbovePlane)
                    {
                        DrawTargetIcon(frame, radarCenterPos, radarPlaneSize, targetInfo, minScale);
                    }

                    DrawRadarText(frame, screenCenter, viewportSize, minScale);
                }
            }

            void DrawRadarText(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 viewportSize, float scale)
            {
                MySprite sprite;
                float textSize = scale * HUD_TEXT_SIZE;
                Vector2 halfScreenSize = viewportSize * 0.5f;
                Vector2 dropShadowOffset = scale * DROP_SHADOW_OFFSET;

                sprite = MySprite.CreateText($"Hostile: {_hostileCount}", FONT, Color.Black, textSize, TextAlignment.CENTER);
                sprite.Data = $"Enemies: {_hostileCount}";
                sprite.Position = screenCenter + new Vector2(-(halfScreenSize.X * 0.5f) + 10, halfScreenSize.Y - (70 * scale)) + dropShadowOffset;
                frame.Add(sprite);
                sprite.Color = Color.Red;
                sprite.Position -= dropShadowOffset;
                frame.Add(sprite);

                sprite.Data = $"Allies: {_allyCount}";
                sprite.Color = Color.Black;
                sprite.Position = screenCenter + new Vector2((halfScreenSize.X * 0.5f) - 10, halfScreenSize.Y - (70 * scale)) + dropShadowOffset;
                frame.Add(sprite);
                sprite.Color = Color.Lime;
                sprite.Position -= dropShadowOffset;
                frame.Add(sprite);
            }

            void DrawLineQuadrantSymmetry(MySpriteDrawFrame frame, Vector2 center, Vector2 point1, Vector2 point2, float width, Color color)
            {
                DrawLine(frame, center + point1, center + point2, width, color);
                DrawLine(frame, center - point1, center - point2, width, color);
                point1.X *= -1;
                point2.X *= -1;
                DrawLine(frame, center + point1, center + point2, width, color);
                DrawLine(frame, center - point1, center - point2, width, color);
            }

            void DrawLine(MySpriteDrawFrame frame, Vector2 point1, Vector2 point2, float width, Color color)
            {
                Vector2 position = 0.5f * (point1 + point2);
                Vector2 diff = point1 - point2;
                float length = diff.Length();
                if (length > 0)
                    diff /= length;

                Vector2 size = new Vector2(length, width);
                float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
                angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

                MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
                sprite.RotationOrScale = angle;
                sprite.Color = color;
                frame.Add(sprite);
            }

            void DrawRadarPlaneBackground(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize)
            {
                MySprite sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize, color: _planeColor);
                sprite.Position = screenCenter;
                frame.Add(sprite);
            }

            void DrawRadarPlane(MySpriteDrawFrame frame, Vector2 viewportSize, Vector2 screenCenter, Vector2 radarScreenCenter, Vector2 radarPlaneSize, float scale)
            {
                MySprite sprite;
                Vector2 halfScreenSize = viewportSize * 0.5f;
                float titleBarHeight = TITLE_BAR_HEIGHT * scale;

                sprite = MySprite.CreateSprite("SquareSimple",
                    screenCenter + new Vector2(0f, -halfScreenSize.Y + titleBarHeight * 0.5f),
                    new Vector2(viewportSize.X, titleBarHeight));
                sprite.Color = _titleBarColor;
                frame.Add(sprite);

                string title = currentTarget.IsEmpty() ? "No Target" : currentTarget.Name;
                sprite = MySprite.CreateText(title, FONT, _textColor, scale * TITLE_TEXT_SIZE, TextAlignment.CENTER);
                sprite.Position = screenCenter + new Vector2(0, -halfScreenSize.Y + 4.25f * scale);
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize * 0.8f, color: _lineColor);
                sprite.Position = radarScreenCenter;
                frame.Add(sprite);

                sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize, color: _lineColor);
                sprite.Position = radarScreenCenter;
                frame.Add(sprite);

                var iconSize = SHIP_ICON_SIZE * scale;
                sprite = new MySprite(SpriteType.TEXTURE, "Triangle", size: iconSize, color: _lineColor);
                sprite.Position = radarScreenCenter + new Vector2(0f, -0.2f * iconSize.Y);
                frame.Add(sprite);

                Vector2 quadrantLine = radarPlaneSize.X * _quadrantLineDirection;
                if (_drawQuadrants)
                {
                    float lineWidth = QUADRANT_LINE_WIDTH * scale;
                    DrawLineQuadrantSymmetry(frame, radarScreenCenter, 0.2f * quadrantLine, 1.0f * quadrantLine, lineWidth, _quadrantLineColor);
                }

            }

            void DrawTargetIcon(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize, TargetInfo targetInfo, float scale)
            {
                Vector3 targetPosPixels = targetInfo.Position * new Vector3(1, _radarProjectionCos, _radarProjectionSin) * radarPlaneSize.X * 0.5f;

                Vector2 targetPosPlane = new Vector2(targetPosPixels.X, targetPosPixels.Y);
                Vector2 iconPos = targetPosPlane - targetPosPixels.Z * Vector2.UnitY;

                RoundVector2(ref iconPos);
                RoundVector2(ref targetPosPlane);

                float elevationLineWidth = Math.Max(1f, TGT_ELEVATION_LINE_WIDTH * scale);
                MySprite elevationSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: targetInfo.ElevationColor, size: new Vector2(elevationLineWidth, targetPosPixels.Z));
                elevationSprite.Position = screenCenter + (iconPos + targetPosPlane) * 0.5f;
                RoundVector2(ref elevationSprite.Position);
                RoundVector2(ref elevationSprite.Size);

                Vector2 iconSize = TGT_ICON_SIZE * scale;
                MySprite iconSprite = new MySprite(SpriteType.TEXTURE, targetInfo.Icon, color: targetInfo.IconColor, size: iconSize);
                iconSprite.Position = screenCenter + iconPos;
                RoundVector2(ref iconSprite.Position);
                RoundVector2(ref iconSprite.Size);


                MySprite threatSprite = MySprite.CreateText(targetInfo.ThreatScore.ToString(), color: Color.Black, scale: 0.9f, fontId: "Debug");
                threatSprite.Position = screenCenter + iconPos + new Vector2(20, -10);
                RoundVector2(ref threatSprite.Position);
                RoundVector2(ref threatSprite.Size);

                MySprite iconShadow = iconSprite;
                iconShadow.Color = Color.Black;
                iconShadow.Size += Vector2.One * 2f * (float)Math.Max(1f, Math.Round(scale * 4f));

                iconSize.Y *= _radarProjectionCos;
                MySprite projectedIconSprite = new MySprite(SpriteType.TEXTURE, "Circle", color: targetInfo.ElevationColor, size: iconSize);
                projectedIconSprite.Position = screenCenter + targetPosPlane;
                RoundVector2(ref projectedIconSprite.Position);
                RoundVector2(ref projectedIconSprite.Size);

                bool showProjectedElevation = Math.Abs(iconPos.Y - targetPosPlane.Y) > iconSize.Y;


                Vector2 dropShadowOffset = scale * DROP_SHADOW_OFFSET;
                Vector2 targetHighlight = new Vector2(3, 3);
                if (targetPosPixels.Z >= 0)
                {
                    if (showProjectedElevation)
                        frame.Add(projectedIconSprite);
                    frame.Add(elevationSprite);
                    if (targetInfo.TargetLock)
                    {
                        MySprite iconHighlight = iconShadow;
                        iconHighlight.Size += targetHighlight;
                        iconHighlight.Color = iconSprite.Color;
                        frame.Add(iconHighlight);
                    }
                    frame.Add(iconShadow);
                    frame.Add(iconSprite);
                    if (targetInfo.Icon == "Circle")
                    {
                        frame.Add(threatSprite);
                        threatSprite.Color = targetInfo.IconColor;
                        threatSprite.Position -= dropShadowOffset;
                        frame.Add(threatSprite);
                    }
                }
                else
                {
                    iconSprite.RotationOrScale = MathHelper.Pi;
                    iconShadow.RotationOrScale = MathHelper.Pi;

                    frame.Add(elevationSprite);
                    if (targetInfo.TargetLock)
                    {
                        MySprite iconHighlight = iconShadow;
                        iconHighlight.Size += targetHighlight;
                        iconHighlight.Color = iconSprite.Color;
                        frame.Add(iconHighlight);
                    }
                    frame.Add(iconShadow);
                    frame.Add(iconSprite);
                    if (showProjectedElevation)
                        frame.Add(projectedIconSprite);
                    if (targetInfo.Icon == "Circle")
                    {
                        frame.Add(threatSprite);
                        threatSprite.Color = targetInfo.IconColor;
                        threatSprite.Position -= dropShadowOffset;
                        frame.Add(threatSprite);
                    }
                }

                if (targetInfo.TargetLock)
                {
                    MySprite sprite = MySprite.CreateText($"Target Distance: {targetInfo.Distance:N0}m Speed: {targetInfo.Velocity.Length():N0}", "Debug", targetInfo.IconColor, RANGE_TEXT_SIZE * scale, TextAlignment.CENTER);
                    sprite.Position = screenCenter + new Vector2(0, radarPlaneSize.Y * 0.5f + scale * 4f);
                    frame.Add(sprite);
                }
            }

            void RoundVector2(ref Vector2? vec)
            {
                if (vec.HasValue)
                    vec = new Vector2((float)Math.Round(vec.Value.X), (float)Math.Round(vec.Value.Y));
            }

            void RoundVector2(ref Vector2 vec)
            {
                vec.X = (float)Math.Round(vec.X);
                vec.Y = (float)Math.Round(vec.Y);
            }
        }


        static List<IMyTextSurface> targetLCDs = new List<IMyTextSurface>();
        static IMyTextSurface targetLCD;

        static List<IMyTextSurface> friendLCDs = new List<IMyTextSurface>();
        static IMyTextSurface friendLCD;

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

        void ClearLcd()
        {
            foreach(IMyTextSurface lcd in targetLCDs)
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
                    if(output.Key.Text.Length > 30)
                    {
                        lcd.WriteText(output.Key.Text.Substring(0, 30) + "...\n", true);
                    }
                    else
                    {
                        lcd.WriteText(output.Key.Text + "\n", true);
                    }
                }
            }
        }

        void AddTextSurfaces(IMyTerminalBlock block, List<IMyTextSurface> textSurfaces)
        {
            var textSurface = block as IMyTextSurface;
            if (textSurface != null)
            {
                textSurfaces.Add(textSurface);
                return;
            }

            var surfaceProvider = block as IMyTextSurfaceProvider;
            if (surfaceProvider == null)
                return;

            textSurfaceIni.Clear();
            bool parsed = textSurfaceIni.TryParse(block.CustomData);

            if (!parsed && !string.IsNullOrWhiteSpace(block.CustomData))
            {
                textSurfaceIni.EndContent = block.CustomData;
            }

            int surfaceCount = surfaceProvider.SurfaceCount;
            for (int i = 0; i < surfaceCount; ++i)
            {
                string iniKey = $"{INI_TEXT_SURFACE_TEMPLATE}, {i}";
                bool display = textSurfaceIni.Get(INI_SECTION_TEXT_SURF_PROVIDER, iniKey).ToBoolean(i == 0 && !(block is IMyProgrammableBlock));
                if (display)
                {
                    textSurfaces.Add(surfaceProvider.GetSurface(i));
                }

                textSurfaceIni.Set(INI_SECTION_TEXT_SURF_PROVIDER, iniKey, display);
            }

            string output = textSurfaceIni.ToString();
            if (!string.Equals(output, block.CustomData))
                block.CustomData = output;
        }

        void WriteCustomDataIni()
        {
            generalIni.Set(INI_SECTION_GENERAL, INI_SHOW_ASTEROIDS, showAsteroids);
            generalIni.Set(INI_SECTION_GENERAL, INI_RADAR_NAME, textPanelName);
            generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
            generalIni.Set(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE, rangeOverride);
            generalIni.Set(INI_SECTION_GENERAL, INI_PROJ_ANGLE, projectionAngle);
            generalIni.Set(INI_SECTION_GENERAL, INI_DRAW_QUADRANTS, drawQuadrants);
            generalIni.Set(INI_SECTION_GENERAL, INI_REF_NAME, referenceName);

            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_TITLE_BAR, titleBarColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_TEXT, textColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_BACKGROUND, backColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_RADAR_LINES, lineColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_PLANE, planeColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_ENEMY, enemyIconColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, enemyElevationColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_NEUTRAL, neutralIconColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, neutralElevationColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_FRIENDLY, allyIconColor, generalIni);
            MyIniHelper.SetColor(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, allyElevationColor, generalIni);
            generalIni.SetSectionComment(INI_SECTION_COLORS, "Colors are defined with RGBA");

            string output = generalIni.ToString();
            if (!string.Equals(output, Me.CustomData))
                Me.CustomData = output;
        }

        void ParseCustomDataIni()
        {
            generalIni.Clear();

            if (generalIni.TryParse(Me.CustomData))
            {
                showAsteroids = generalIni.Get(INI_SECTION_GENERAL, INI_SHOW_ASTEROIDS).ToBoolean(showAsteroids);
                textPanelName = generalIni.Get(INI_SECTION_GENERAL, INI_RADAR_NAME).ToString(textPanelName);
                referenceName = generalIni.Get(INI_SECTION_GENERAL, INI_REF_NAME).ToString(referenceName);
                useRangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE).ToBoolean(useRangeOverride);
                rangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE).ToSingle(rangeOverride);
                projectionAngle = generalIni.Get(INI_SECTION_GENERAL, INI_PROJ_ANGLE).ToSingle(projectionAngle);
                drawQuadrants = generalIni.Get(INI_SECTION_GENERAL, INI_DRAW_QUADRANTS).ToBoolean(drawQuadrants);

                titleBarColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_TITLE_BAR, generalIni, titleBarColor);
                textColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_TEXT, generalIni, textColor);
                backColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_BACKGROUND, generalIni, backColor);
                lineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_RADAR_LINES, generalIni, lineColor);
                planeColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_PLANE, generalIni, planeColor);
                enemyIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_ENEMY, generalIni, enemyIconColor);
                enemyElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, generalIni, enemyElevationColor);
                neutralIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_NEUTRAL, generalIni, neutralIconColor);
                neutralElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, generalIni, neutralElevationColor);
                allyIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_FRIENDLY, generalIni, allyIconColor);
                allyElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, generalIni, allyElevationColor);
            }
            else if (!string.IsNullOrWhiteSpace(Me.CustomData))
            {
                generalIni.EndContent = Me.CustomData;
            }

            WriteCustomDataIni();

            if (radarSurface != null)
            {
                radarSurface.UpdateFields(titleBarColor, backColor, lineColor, planeColor, textColor, missileLockColor, projectionAngle, MaxRange, drawQuadrants);
            }
        }

        public static class MyIniHelper
        {
            public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
            {

                string colorString = $"{color.R}, {color.G}, {color.B}, {color.A}";

                ini.Set(sectionName, itemName, colorString);
            }

            public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
            {
                string rgbString = ini.Get(sectionName, itemName).ToString("null");
                string[] rgbSplit = rgbString.Split(',');

                int r = 0;
                int g = 0;
                int b = 0;
                int a = 0;
                if (rgbSplit.Length != 4)
                {
                    if (defaultChar.HasValue)
                        return defaultChar.Value;
                    else
                        return Color.Transparent;
                }

                int.TryParse(rgbSplit[0].Trim(), out r);
                int.TryParse(rgbSplit[1].Trim(), out g);
                int.TryParse(rgbSplit[2].Trim(), out b);
                bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
                if (!hasAlpha)
                    a = 255;

                r = MathHelper.Clamp(r, 0, 255);
                g = MathHelper.Clamp(g, 0, 255);
                b = MathHelper.Clamp(b, 0, 255);
                a = MathHelper.Clamp(a, 0, 255);

                return new Color(r, g, b, a);
            }
        }


        IMyShipController GetControlledShipController(List<IMyShipController> SCs)
        {
            foreach (IMyShipController thisController in SCs)
            {
                if (IsClosed(thisController))
                    continue;

                if (thisController.IsUnderControl && thisController.CanControlShip)
                    return thisController;
            }

            return null;
        }

        public static bool IsClosed(IMyTerminalBlock block)
        {
            return block.WorldMatrix == MatrixD.Identity;
        }

        public static bool StringContains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        bool PopulateLists(IMyTerminalBlock block)
        {
            if (!block.IsSameConstructAs(Me))
                return false;

            if (StringContains(block.CustomName, textPanelName))
            {
                AddTextSurfaces(block, textSurfaces);
            }

            if (block.CustomName.Contains("Target LCD"))
            {
                targetLCD = block as IMyTextSurface;
                if (targetLCD != null)
                {
                    targetLCD.ContentType = ContentType.TEXT_AND_IMAGE;
                    targetLCD.BackgroundColor = Color.Black;
                    targetLCD.FontColor = Color.Red;
                    targetLCDs.Add(targetLCD);
                    return false;
                }
            }

            if (block.CustomName.Contains("Friend LCD"))
            {
                friendLCD = block as IMyTextSurface;
                if (friendLCD != null)
                {
                    friendLCD.ContentType = ContentType.TEXT_AND_IMAGE;
                    friendLCD.BackgroundColor = Color.Black;
                    friendLCD.FontColor = Color.Green;
                    friendLCDs.Add(friendLCD);
                    return false;
                }
            }

            if (block is IMySoundBlock && block.CustomName.ToLower().Contains("alert"))
            {
                soundBlocks.Add(block as IMySoundBlock);
                return false;
            }

            if (block is IMyLightingBlock && block.CustomName.ToLower().Contains("alert"))
            {
                warningLights.Add(block as IMyLightingBlock);
                return false;
            }

            if (wcapi.HasCoreWeapon(block))
            {
                turrets.Add(block);
                return false;
            }

            var controller = block as IMyShipController;
            if (controller != null)
            {
                allControllers.Add(controller);
                if (StringContains(block.CustomName, referenceName))
                    taggedControllers.Add(controller);
                return false;
            }

            var sensor = block as IMySensorBlock;
            if (sensor != null)
            {
                sensors.Add(sensor);
                return false;
            }

            return false;
        }

        void GrabBlocks()
        {
            if (!wcapiActive)
            {
                try
                {
                    wcapiActive = wcapi.Activate(Me);
                }
                catch
                {
                    wcapiActive = false;
                    return;
                }
            }

            _clearSpriteCache = !_clearSpriteCache;

            myGridIds.Clear();
            sensors.Clear();
            turrets.Clear();
            allControllers.Clear();
            taggedControllers.Clear();
            textSurfaces.Clear();
            targetLCDs.Clear();
            friendLCDs.Clear();

            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, PopulateLists);

            StringBuilder sb = new StringBuilder();

            if (turrets.Count == 0)
                sb.AppendLine($"No turrets found. Radar will not function without a weapon core turret on your grid.");

            if (!wcapiActive)
                sb.AppendLine($"ERROR: WC API NOT ACTIVATED");

            if (textSurfaces.Count == 0)
                sb.AppendLine($"No text panels or text surface providers with name tag '{textPanelName}' were found.");

            if (allControllers.Count == 0)
                sb.AppendLine($"No ship controllers were found. Using orientation of this block...");
            else
            {
                if (taggedControllers.Count == 0)
                    sb.AppendLine($"No ship controllers named '{referenceName}' were found. Using all available ship controllers. (This is NOT an error!)");
                else
                    sb.AppendLine($"One or more ship controllers with name tag '{referenceName}' were found. Using these to orient the radar.");
            }

            lastSetupResult = sb.ToString();

            if (textSurfaces.Count == 0 && targetLCDs.Count == 0 && friendLCDs.Count == 0)
                isSetup = false;
            else
            {
                isSetup = true;
                ParseCustomDataIni();
            }
        }

        public class RuntimeTracker
        {
            public int Capacity { get; set; }
            public double Sensitivity { get; set; }
            public double MaxRuntime { get; private set; }
            public double MaxInstructions { get; private set; }
            public double AverageRuntime { get; private set; }
            public double AverageInstructions { get; private set; }
            public double LastRuntime { get; private set; }
            public double LastInstructions { get; private set; }

            readonly Queue<double> _runtimes = new Queue<double>();
            readonly Queue<double> _instructions = new Queue<double>();
            readonly StringBuilder _sb = new StringBuilder();
            readonly int _instructionLimit;
            readonly Program _program;
            const double MS_PER_TICK = 16.6666;

            public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
            {
                _program = program;
                Capacity = capacity;
                Sensitivity = sensitivity;
                _instructionLimit = _program.Runtime.MaxInstructionCount;
            }

            public void AddRuntime()
            {
                double runtime = _program.Runtime.LastRunTimeMs;
                LastRuntime = runtime;
                AverageRuntime += (Sensitivity * runtime);
                int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
                if (roundedTicksSinceLastRuntime == 1)
                {
                    AverageRuntime *= (1 - Sensitivity);
                }
                else if (roundedTicksSinceLastRuntime > 1)
                {
                    AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
                }

                _runtimes.Enqueue(runtime);
                if (_runtimes.Count == Capacity)
                {
                    _runtimes.Dequeue();
                }

                MaxRuntime = _runtimes.Max();
            }

            public void AddInstructions()
            {
                double instructions = _program.Runtime.CurrentInstructionCount;
                LastInstructions = instructions;
                AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

                _instructions.Enqueue(instructions);
                if (_instructions.Count == Capacity)
                {
                    _instructions.Dequeue();
                }

                MaxInstructions = _instructions.Max();
            }

            public string Write()
            {
                _sb.Clear();
                _sb.AppendLine("General Runtime Info");
                _sb.AppendLine($"  Avg instructions: {AverageInstructions:n2}");
                _sb.AppendLine($"  Last instructions: {LastInstructions:n0}");
                _sb.AppendLine($"  Max instructions: {MaxInstructions:n0}");
                _sb.AppendLine($"  Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
                _sb.AppendLine($"  Avg runtime: {AverageRuntime:n4} ms");
                _sb.AppendLine($"  Last runtime: {LastRuntime:n4} ms");
                _sb.AppendLine($"  Max runtime: {MaxRuntime:n4} ms");
                return _sb.ToString();
            }
        }

        public class Scheduler
        {
            readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
            readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
            Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
            const double runtimeToRealtime = (1.0 / 60.0) / 0.0166666;
            private readonly Program _program;
            private ScheduledAction _currentlyQueuedAction = null;

            public Scheduler(Program program)
            {
                _program = program;
            }

            public void Update()
            {
                double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * runtimeToRealtime);

                _actionsToDispose.Clear();
                foreach (ScheduledAction action in _scheduledActions)
                {
                    action.Update(deltaTime);
                    if (action.JustRan && action.DisposeAfterRun)
                    {
                        _actionsToDispose.Add(action);
                    }
                }

                _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

                if (_currentlyQueuedAction == null)
                {
                    if (_queuedActions.Count != 0)
                        _currentlyQueuedAction = _queuedActions.Dequeue();
                }

                if (_currentlyQueuedAction != null)
                {
                    _currentlyQueuedAction.Update(deltaTime);
                    if (_currentlyQueuedAction.JustRan)
                    {
                        if (!_currentlyQueuedAction.DisposeAfterRun)
                            _queuedActions.Enqueue(_currentlyQueuedAction);

                        _currentlyQueuedAction = null;
                    }
                }
            }

            public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false)
            {
                ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun);
                _scheduledActions.Add(scheduledAction);
            }

            public void AddScheduledAction(ScheduledAction scheduledAction)
            {
                _scheduledActions.Add(scheduledAction);
            }

            public void AddQueuedAction(Action action, double updateInterval, bool disposeAfterRun = false)
            {
                if (updateInterval <= 0)
                {
                    updateInterval = 0.001;
                }
                ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, disposeAfterRun);
                _queuedActions.Enqueue(scheduledAction);
            }

            public void AddQueuedAction(ScheduledAction scheduledAction)
            {
                _queuedActions.Enqueue(scheduledAction);
            }

            public void RemoveRunningAction()
            {
                foreach (ScheduledAction action in _scheduledActions)
                {
                    if (action.Running)
                    {
                        _actionsToDispose.Add(action);
                        return;
                    }
                }
            }

            public void AddScheduledActionSafe(Action action, double updateFrequency, bool disposeAfterRun = false)
            {
                ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun);
                ScheduledAction queueAddition = new ScheduledAction(delegate { AddScheduledAction(scheduledAction); }, 0.001, true);
                AddQueuedAction(queueAddition);
            }

            public void AddScheduledActionSafe(ScheduledAction scheduledAction)
            {
                ScheduledAction queueAddition = new ScheduledAction(delegate { AddScheduledAction(scheduledAction); }, 0.001, true);
                AddQueuedAction(queueAddition);
            }
        }

        public class ScheduledAction
        {
            public bool JustRan { get; private set; } = false;
            public bool DisposeAfterRun { get; private set; } = false;
            public double TimeSinceLastRun { get; private set; } = 0;
            public readonly double RunInterval;
            public bool Running { get; private set; } = false;

            private readonly double _runFrequency;
            private readonly Action _action;
            protected bool _justRun = false;

            public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false)
            {
                _action = action;
                _runFrequency = runFrequency;
                RunInterval = 1.0 / _runFrequency;
                DisposeAfterRun = removeAfterRun;
            }

            public virtual void Update(double deltaTime)
            {
                TimeSinceLastRun += deltaTime;

                if (TimeSinceLastRun >= RunInterval)
                {
                    Running = true;
                    _action.Invoke();
                    TimeSinceLastRun = 0;

                    Running = false;
                    JustRan = true;
                }
                else
                {
                    JustRan = false;
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
