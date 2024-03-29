using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // main cockpit is used for orientation, make sure you have ONE.
        // artificial masses should be near the center of mass, otherwise the drive will rotate the ship

        // --------------- IMPORTANT SETTINGS ---------------
        // maximun allowed average ms per tick, 0.3 ms is allowed on most servers
        double maxMs = 0.3;       
        // --------------- IMPORTANT SETTINGS ---------------

        // --------- other settings ---------
        // dampening will be disabled below this speed
        int lowestSpeed = 0;
        // automatically set the field size of the gravity generators
        bool autoFieldSize = true;
        // run every 1, 10 or 100 tick, 1 tick = 16.6 ms
        // 1 is probably fine, use 10 if it somehow overheats. 
        // lower value = higher ms usage per tick
        int updateFrequency = 1;
        // --------- other settings ---------

        // no touchie below this point unless you know what you're doing ------------------------------------------------

        IMyCockpit cockpit;
        List<IMyCockpit> cockpits = new List<IMyCockpit>();
        List<IMyGravityGenerator>[,] gens = new List<IMyGravityGenerator>[2, 6]; // 6 lists, one for each orientation
        List<IMyArtificialMassBlock>[] masses = new List<IMyArtificialMassBlock>[2]; // 2 lists, front and back
        List<IMyGravityGenerator> allGens = new List<IMyGravityGenerator>();
        List<IMyArtificialMassBlock> allMasses = new List<IMyArtificialMassBlock>();
        List<IMyGyro> gyros = new List<IMyGyro>();
        IMyTextSurface screen;
        double gyroTorque = 33.6; // in MNn
        Vector3I fieldMax, fieldMin;

        public Program()
        {
            // get necessary blocks from grid and check for cockpit
            Echo("Loading...");
            try
            {
                GridTerminalSystem.GetBlocksOfType(cockpits);
                int maincockpitcount = 0;
                foreach (IMyCockpit mainCockpit in cockpits)
                {
                    if (mainCockpit.IsMainCockpit)
                    {
                        maincockpitcount++;
                        Echo("Found main cockpit.");
                        cockpit = mainCockpit;
                    }
                }
                if (cockpit == null)
                {
                    Echo("No main cockpit was found, please add one and recompile.");
                    return;
                }
                if (maincockpitcount > 1)
                {
                    Echo("More than 1 main cockpit was found, please make sure there is only 1 and recompile.");
                    return;
                }
                screen = cockpit.GetSurface(0);
                screen.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                screen.FontSize = 1.2f;
            }
            catch
            {
                Echo("No cockpit was found, please add one and recompile.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(allMasses);
            GridTerminalSystem.GetBlocksOfType(allGens);
            GridTerminalSystem.GetBlocksOfType(gyros);

            if (allGens.Count == 0)
            {
                Echo("No gravity generators were found, please add some and recompile.");
                return;
            }
            if (allMasses.Count == 0)
            {
                Echo("No artificial masses were found, please add some and recompile.");
                return;
            }

            Echo("Gravity generators: " + allGens.Count.ToString() + "\nArtificial masses: " + allMasses.Count.ToString());

            // init lists for gens
            for (int half = 0; half < 2; half++)
            {
                for (int orientation = 0; orientation < 6; orientation++)
                {
                    gens[half, orientation] = new List<IMyGravityGenerator>();
                }
            }
            for (int x = 0; x < 2; x++)
            {
                masses[x] = new List<IMyArtificialMassBlock>();
            }

            // sort gens into lists by orientation
            for (int i = 0; i < allGens.Count; i++)
            {
                switch (cockpits[0].Orientation.TransformDirectionInverse(allGens[i].Orientation.Up).ToString())
                {
                    case "Up":
                        gens[0,0].Add(allGens[i]);
                        break;
                    case "Down":
                        gens[0,1].Add(allGens[i]);
                        break;
                    case "Right":
                        gens[0,2].Add(allGens[i]);
                        break;
                    case "Left":
                        gens[0,3].Add(allGens[i]);
                        break;
                    case "Backward":
                        gens[0,4].Add(allGens[i]);
                        break;
                    case "Forward":
                        gens[0,5].Add(allGens[i]);
                        break;
                }
            }
            Echo("Ready!");
            switch (updateFrequency)
            {
                case 1:
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    break;
                case 10:
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    break;
                case 100:
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    break;
                default:
                    Echo("Incorrect or no updatefrequency set, select one and recompile.");
                    return;
            }
            SetFieldSize();
        }

        Vector3D moveInd = new Vector3D();
        Vector3D velVect = new Vector3D();     
        double averageRuntime = 0;

        public void Main(string argument, UpdateType updateSource)
        {
            ResetGyro();
            Echo(fieldMax.ToString() + "\n"+ fieldMin.ToString());
            screen.WriteText("");
            /*Echo("Gravdrive");
            Echo("Gravity generators: " + allGens.Count.ToString());
            Echo("Artificial masses: " + allMasses.Count.ToString());
            Echo("UpdateFrequency: " + updateFrequency.ToString() + ", every " + (updateFrequency*16.6).ToString() + " ms");
            Echo("Dampeners: " + cockpit.DampenersOverride.ToString());
            screen.WriteText("Efficiency: " + Math.Round((100 - (cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%\n");
            Echo("Efficiency: " + Math.Round((100-(cockpit.GetNaturalGravity().Length() / 9.81 * 100 * 2)), 2) + "%");*/          

            if (Math.Round(cockpit.GetShipSpeed(), 2) == 0 && NoPilotInput())
            {
                screen.WriteText("Velocity: Stationary\nStatus: Standby", true);
                Echo("Velocity: Stationary\nStatus: Standby");
                PowerOnOff(false);
            }
            else if (!cockpit.DampenersOverride && NoPilotInput())
            {
                screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipVelocities().LinearVelocity.Length(), 2).ToString() + " m/s\nStatus: Drifting", true);
                Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: Drifting");
                PowerOnOff(false);
            }
            else
            {
                string status = "Active";
                if (NoPilotInput())
                {
                    status = "Braking";
                }
                screen.WriteText("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status, true);
                Echo("Velocity: " + Math.Round(cockpit.GetShipSpeed(), 2).ToString() + " m/s\nStatus: " + status);
                PowerOnOff(true);
            }

            // vector of momentum in each direction, set to 0 if user input is detected in said direction or if dampeners are off (could be checked later but easier to integrate here)
            velVect.X = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).X * IsZero(cockpit.MoveIndicator.X) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
            velVect.Y = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Y * IsZero(cockpit.MoveIndicator.Y) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();
            velVect.Z = Vector3D.TransformNormal(cockpit.GetShipVelocities().LinearVelocity, MatrixD.Transpose(cockpit.WorldMatrix)).Z * IsZero(cockpit.MoveIndicator.Z) * Convert.ToInt32(cockpit.DampenersOverride) * UnderLowestSpeed();

            moveInd = cockpit.MoveIndicator * 9.8f;
            Vector3D forceVector = new Vector3D();

            // set power value for each gravity drive in each orientation
            for (int half = 0; half < 2; half++)
            {
                for (int orientation = 0; orientation < 6; orientation++)
                {
                    foreach (IMyGravityGenerator gen in gens[half, orientation])
                    {
                        switch (cockpit.Orientation.TransformDirectionInverse(gen.Orientation.Up))
                        {
                            case Base6Directions.Direction.Up:
                                gen.GravityAcceleration = -GravAccerelation(moveInd.Y, -velVect.Y);
                                forceVector.Y += -allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                            case Base6Directions.Direction.Down:
                                gen.GravityAcceleration = GravAccerelation(moveInd.Y, -velVect.Y);
                                forceVector.Y += allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                            case Base6Directions.Direction.Right:
                                gen.GravityAcceleration = -GravAccerelation(moveInd.X, -velVect.X);
                                forceVector.X += -allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                            case Base6Directions.Direction.Left:
                                gen.GravityAcceleration = GravAccerelation(moveInd.X, -velVect.X);
                                forceVector.X += allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                            case Base6Directions.Direction.Backward:
                                gen.GravityAcceleration = -GravAccerelation(moveInd.Z, -velVect.Z);
                                forceVector.Z += -allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                            case Base6Directions.Direction.Forward:
                                gen.GravityAcceleration = GravAccerelation(moveInd.Z, -velVect.Z);
                                forceVector.Z += allMasses.Count * allMasses[0].VirtualMass * gen.GravityAcceleration;
                                break;
                        }
                    }                
                }
            }

            SetGyro();

            Vector3D massCenterVector = new Vector3D();

            // add together locations of mass blocks relative to the center of mass of the ship, use cockpit orientation as reference
            for (int i = 0; i < allMasses.Count; i++)
            {
                massCenterVector += -Vector3D.TransformNormal(allMasses[i].GetPosition() - cockpit.CenterOfMass, MatrixD.Transpose(cockpit.WorldMatrix));
            }
            // average location to get the center point of them (point of applied force on ship)
            massCenterVector /= allMasses.Count;

            SplitIntoTwo(massCenterVector);

            Vector3D torqueVector = Vector3D.Cross(massCenterVector, forceVector);

            // mostly for debugging
            Echo(massCenterVector.X.ToString());
            Echo(massCenterVector.Y.ToString());
            Echo(massCenterVector.Z.ToString());
            /*
            screen.WriteText("X: " + Math.Round(massCenterVector.X, 2).ToString() + " MN\n", true);
            screen.WriteText("Y: " + Math.Round(massCenterVector.Y, 2).ToString() + " MN\n", true);
            screen.WriteText("Z: " + Math.Round(massCenterVector.Z, 2).ToString() + " MN\n", true);

            screen.WriteText("X: " + Math.Round(forceVector.X / 1000000, 2).ToString() + " MN\n", true);
            screen.WriteText("Y: " + Math.Round(forceVector.Y / 1000000, 2).ToString() + " MN\n", true);
            screen.WriteText("Z: " + Math.Round(forceVector.Z / 1000000, 2).ToString() + " MN\n", true);
            */
            screen.WriteText("\n" + Math.Round(torqueVector.X / 1000000).ToString() + " X\n", true);
            screen.WriteText(Math.Round(torqueVector.Y / 1000000).ToString() + " Y\n", true);
            screen.WriteText(Math.Round(torqueVector.Z / 1000000).ToString() + " Z\n", true);
            
            // total torque caused by the gravity drive, in MNm, probably useless
            double totalTorque = Math.Abs((forceVector.X * massCenterVector.Length() + forceVector.Y * massCenterVector.Length()) / 1000000);
            int neededGyros = (int)Math.Ceiling(totalTorque / gyroTorque);
            screen.WriteText(Math.Round(torqueVector.Length()/1000000).ToString() + "\n", true);
            //screen.WriteText(Math.Round(totalTorque, 1).ToString() + " MNn (torque)\n", true);
            //screen.WriteText(neededGyros.ToString() + " gyros needed\n", true);

            screen.WriteText(Math.Round(Math.Abs(torqueVector.Y / 1000000 / (gyros.Count * gyroTorque)), 4).ToString() + " power\n", true);

            // average runtime per tick, script runs every updateFrequency ticks, so divide by that
            averageRuntime = averageRuntime * 0.99 + (((Runtime.LastRunTimeMs / updateFrequency) * 0.01));
            if(averageRuntime > maxMs*0.85)
            {
                Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " ms\nSome functions are slowed\nto prevent overheating.");
                screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " ms\nSome functions are slowed\nto prevent overheating.", true);
            }
            else
            {
                Echo("Runtime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms");
                screen.WriteText("\nRuntime: " + Math.Round(averageRuntime, 4).ToString() + " / " + maxMs.ToString() + " ms", true);
            }
        }
        public void PowerOnOff(bool power)
        {
            if (averageRuntime < maxMs * 0.85)
            {
                foreach (IMyArtificialMassBlock mass in allMasses)
                {
                    mass.Enabled = power;
                }
                foreach (IMyGravityGenerator gen in allGens)
                {
                    gen.Enabled = power;
                }
            }
        }
        public bool NoPilotInput()
        {
            return cockpit.MoveIndicator.X == 0 && cockpit.MoveIndicator.Y == 0 && cockpit.MoveIndicator.Z == 0;
        }
        public float GravAccerelation(double LinCtrl, double velVec)
        {
            return (float)(LinCtrl + velVec);
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
            if(cockpit.GetShipVelocities().LinearVelocity.Length() < lowestSpeed)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }
        public void SplitIntoTwo(Vector3D massVector)
        {
            foreach(List<IMyArtificialMassBlock> list in masses)
            {
                list.Clear();
            }
            foreach(IMyArtificialMassBlock massBlock in allMasses)
            {
                if ((-Vector3D.TransformNormal(massBlock.GetPosition() - cockpit.CenterOfMass, MatrixD.Transpose(cockpit.WorldMatrix))).Z > massVector.Z )
                {
                    masses[0].Add(massBlock);
                }
                else
                {
                    masses[1].Add(massBlock);
                }
            }
            foreach(List<IMyGravityGenerator> gen in gens)
            {
                
            }
            Echo(masses[0].Count.ToString());
            Echo(masses[1].Count.ToString());
        }
        public void SetFieldSize()
        {
            if(fieldMax == Vector3I.Zero || fieldMin == Vector3I.Zero)
            {
                foreach (IMyArtificialMassBlock mass in allMasses)
                {
                    if (fieldMax.X > mass.Max.X) fieldMax.X = mass.Max.X;
                    if (fieldMax.Y > mass.Max.Y) fieldMax.Y = mass.Max.Y;
                    if (fieldMax.Z > mass.Max.Z) fieldMax.Z = mass.Max.Z;

                    if (fieldMin.X < mass.Min.X) fieldMin.X = mass.Min.X;
                    if (fieldMin.Y < mass.Min.Y) fieldMin.Y = mass.Min.Y;
                    if (fieldMin.Z < mass.Min.Z) fieldMin.Z = mass.Min.Z;
                }
            }
            if (autoFieldSize)
            {
                foreach (IMyGravityGenerator gen in allGens)
                {
                    var height = GetLengthToCorner(gen.Orientation.Up, fieldMax, fieldMin, gen.Position);
                    var width = GetLengthToCorner(gen.Orientation.Left, fieldMax, fieldMin, gen.Position);
                    var depth = GetLengthToCorner(gen.Orientation.Forward, fieldMax, fieldMin, gen.Position);
                    gen.FieldSize = new Vector3(width * 5 + 3, height * 5 + 3, depth * 5 + 3);
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
                default: throw new NullReferenceException("script brokie");
            }
        }
    }
}
