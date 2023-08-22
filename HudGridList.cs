        // lists nearby grids to lcds and includes whips modified weaponcore radar
        // name LCDs "Friend LCD", "Target LCD" and "Radar LCD" (non case sensitive)
        // radar does NOT work with hudlcd
        // --Asmoww

        // general settings
        double maxMs = 0.3; // throttle the script if runtime exceeds this      

        // gridlist settings
        bool sortByDistance = true; // sort by threat if disabled 
        int colorDistance = 6000; // at what distance should colors start be applied to distance number
        int maxNameLenght = 25; // how long grid names are allowed to be without cutting them off
        int maxEntries = 15; // max amount of grids to display at a time
        bool displayEmpty = false; // make the lcds display no enemies or friendlies nearby
        bool hideNonimportant = true; // hide not-so-important enemy grids, threat level of 0.1 or lower, no movement or freely drifting
        bool approachWarning = true; // warn for approaching grids with sound block
        int approachDistance = 1500; // distance in meters, warn if grid is approaching within specified distance
        string warningSound = "SoundBlockAlert2";       
        Color friendColor = Color.Green;
        Color enemyColor = Color.IndianRed;
        Color targetingColor = Color.Orange;

        // radar settings
        bool drawQuadrants = false;
        float projectionAngle = 50f;
        float rangeOverride = 15000;

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


        // code below code below code below code below code below code below code below code below code below code below code below


        public static WcPbApi wcapi = new WcPbApi();
        Dictionary<MyDetectedEntityInfo, float> wcTargets = new Dictionary<MyDetectedEntityInfo, float>();
        List<MyDetectedEntityInfo> wcObstructions = new List<MyDetectedEntityInfo>();
        Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
        Dictionary<long, double> prevDistances = new Dictionary<long, double>();
        Dictionary<long, VRageMath.Vector3D> prevVelocities = new Dictionary<long, VRageMath.Vector3D>();
        Dictionary<long, VRageMath.MatrixD> prevAngles = new Dictionary<long, VRageMath.MatrixD>();
        static MyDetectedEntityInfo currentTarget;
        bool approaching = false;
        bool soundPlayed = false;
        int tickNum = 0;
        double averageRuntime = 0;
        static double tickSpeed = 1/6; //seconds per tick
        IMyTerminalBlock reference;
       
        bool useRangeOverride = true;
        bool showAsteroids = false;
        float MaxRange
        {
            get
            {
                return rangeOverride;
            }
        }

        readonly RadarSurface radarSurface;
        List<IMyShipController> allControllers = new List<IMyShipController>();
        IMyShipController lastActiveShipController = null;

        List<IMyShipController> Controllers
        {
            get
            {
                return allControllers;
            }
        }

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
            Echo("starting");
            radarSurface = new RadarSurface(titleBarColor, backColor, lineColor, planeColor, textColor, missileLockColor, projectionAngle, MaxRange, drawQuadrants);
            Echo("heloasd");
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
            Echo((friendLCDs.Count + targetLCDs.Count + radarLCDs.Count).ToString() + " LCDs");
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
            radarSurface.SortContacts();
            foreach (IMyTextSurface lcd in radarLCDs)
            {
                var lcdtemp = lcd;
                radarSurface.DrawRadar(lcdtemp, false);
            }
        }

        Dictionary<String, double> targetOutput = new Dictionary<String, double>();
        Dictionary<String, double> friendOutput = new Dictionary<String, double>();

        static List<IMyTextPanel> allLCDs = new List<IMyTextPanel>();
        static List<IMyTextSurface> targetLCDs = new List<IMyTextSurface>();
        static List<IMyTextSurface> friendLCDs = new List<IMyTextSurface>();
        static List<IMyTextSurface> radarLCDs = new List<IMyTextSurface>();

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
                    }
                    if (target.Color == targetingColor)
                        warning = "@" + warning;

                    if (target.MyTarget)
                    {
                        warning = ">" + warning;
                    }

                    string tempTargetName = target.Info.Name;

                    if (target.Info.Name.Length > maxNameLenght)
                    {
                        tempTargetName = target.Info.Name.Substring(0, maxNameLenght);
                    }

                    target.DistanceColor = Color.DimGray;
   
                    if (target.Info.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                    {
                        if (prevVelocities.ContainsKey(obj.Key) && prevAngles.ContainsKey(obj.Key) && hideNonimportant)
                        {
                            if (target.Info.Velocity == prevVelocities[obj.Key] && target.Info.Orientation == prevAngles[obj.Key] && target.Threat < 0.1)
                            {
                                continue;
                            }
                        }

                        if (target.Distance <= colorDistance)
                        {
                            target.DistanceColor = Color.Gray;
                            if (target.Distance <= colorDistance - (colorDistance / 6)) target.DistanceColor = Color.LightGray;
                            if (target.Distance <= colorDistance - (2 * colorDistance / 6)) target.DistanceColor = Color.LightYellow;
                            if (target.Distance <= colorDistance - (3 * colorDistance / 6)) target.DistanceColor = Color.Yellow;
                            if (target.Distance <= colorDistance - (4 * colorDistance / 6)) target.DistanceColor = Color.Orange;
                            if (target.Distance <= colorDistance - (5 * colorDistance / 6)) target.DistanceColor = Color.Red;
                        }
                        if(targetOutput.Count() <= maxEntries) targetOutput.Add(ColorToColor(target.Color) + warning + " " + tempTargetName.ToString() + " " + ColorToColor(target.DistanceColor) + Math.Round(target.Distance / 1000, 2).ToString() + "km", sorter);
                    }
                    else
                    {
                        if (target.Distance <= colorDistance)
                        {
                            target.DistanceColor = Color.Gray;
                            if (target.Distance <= colorDistance - (colorDistance / 6)) target.DistanceColor = Color.DarkGray;
                            if (target.Distance <= colorDistance - (2 * colorDistance / 6)) target.DistanceColor = Color.LightGray;
                            if (target.Distance <= colorDistance - (4 * colorDistance / 6)) target.DistanceColor = Color.White;
                        }
                        string friendlyType = "";
                        if (target.Info.Name.ToString() == "")
                            friendlyType = "Suit";
                        if (friendOutput.Count() <= maxEntries) friendOutput.Add(ColorToColor(target.Color) + tempTargetName.ToString() + " " + ColorToColor(target.DistanceColor) + Math.Round(target.Distance / 1000, 2).ToString() + "km" + friendlyType, sorter);
                    }
                }
                catch { }
            }
            if (displayEmpty)
            {
                if (targetOutput.Count() == 0) targetOutput.Add(ColorToColor(Color.DarkRed) + "No enemies :)", 0);
                if (friendOutput.Count() == 0) friendOutput.Add(ColorToColor(Color.DarkGreen) + "No friends :(", 0);
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
            radarSurface.ClearContacts();
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

            foreach (var kvp in targetDataDict)
            {
                if (kvp.Key == Me.CubeGrid.EntityId)
                    continue;

                var targetData = kvp.Value;
                RadarSurface.Relation relation = RadarSurface.Relation.Neutral;
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
                        relation = RadarSurface.Relation.Hostile;
                        targetData.Color = enemyColor;
                        if (approachWarning && targetData.Distance < approachDistance && targetData.MyTarget == false && targetData.Info.Type != MyDetectedEntityType.CharacterHuman)
                        {
                            if (targetData.ApproachSpeed > 3 || targetData.Distance < (approachDistance / 2))
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
                        relation = RadarSurface.Relation.Allied;
                        targetData.Color = friendColor;
                        break;
                    default:
                        targetData.Threat = -1;
                        if (targetData.Info.Type == MyDetectedEntityType.Unknown)
                        {
                            relation = RadarSurface.Relation.Allied;
                            targetData.Color = friendColor;
                        }
                        break;
                }

                if (targetData.Info.Type == MyDetectedEntityType.CharacterHuman)
                {
                    targetData.Color = SuitColor(targetData.Color);
                }

                temp[targetData.Info.EntityId] = targetData;
                radarSurface.AddContact(targetData.Info.Position, reference.WorldMatrix, targetData.Info.Type, targetData.Color, targetData.Color, targetData.Info.Name, relation, targetData.MyTarget, targetData.Distance, targetData.Info.Velocity, targetData.Threat);
            }

            targetDataDict.Clear();
            foreach (var item in temp)
                targetDataDict[item.Key] = item.Value;
            foreach (var item in targetDataDict)
            {
                prevDistances[item.Key] = item.Value.Distance;
                prevVelocities[item.Key] = item.Value.Info.Velocity;
                prevAngles[item.Key] = item.Value.Info.Orientation;
            }
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
            radarLCDs.Clear();
            cockpits.Clear();
            allControllers.Clear();
            soundblocks.Clear();
            GridTerminalSystem.GetBlocksOfType(allLCDs);
            GridTerminalSystem.GetBlocksOfType(cockpits);
            GridTerminalSystem.GetBlocksOfType(soundblocks);
            GridTerminalSystem.GetBlocksOfType(allControllers);
            foreach (IMyTextPanel lcd in allLCDs)
            {
                if (lcd.CustomName.ToLower().Contains("friend") && lcd.IsSameConstructAs(Me))
                {
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.BackgroundColor = Color.Black;
                    friendLCDs.Add(lcd);
                    if (!lcd.CustomData.Contains("hudlcd")) lcd.CustomData = "hudlcd:-0.98:0.98";
                }
                else if (lcd.CustomName.ToLower().Contains("target") && lcd.IsSameConstructAs(Me))
                {
                    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                    lcd.BackgroundColor = Color.Black;
                    targetLCDs.Add(lcd);
                    if (!lcd.CustomData.Contains("hudlcd")) lcd.CustomData = "hudlcd:-0.7:0.98";
                }
                else if (lcd.CustomName.ToLower().Contains("radar") && lcd.IsSameConstructAs(Me))
                {
                    lcd.ContentType = ContentType.SCRIPT;
                    lcd.BackgroundColor = Color.Black;
                    radarLCDs.Add(lcd);
                }
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
