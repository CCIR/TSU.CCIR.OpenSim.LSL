using System;
using System.Reflection;
using System.Collections.Generic;

using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.Physics.Manager;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

using TeessideUniversity.CCIR.OpenSim;

[assembly: Addin("TSU.CCIR.OpenSim.LSL", "0.1")]
[assembly: AddinDependency("OpenSim", "0.7.5")]

namespace TeessideUniversity.CCIR.OpenSim
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "TSU.CCIR.OpenSim.LSL")]
    class TSUCCIRSL : INonSharedRegionModule
    {

        #region logging

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion

        private Scene m_scene;
        private IScriptModuleComms m_scriptModuleComms;

        bool m_enabled = false;

        #region INonSharedRegionModule

        public string Name
        {
            get { return "TSU.CCIR.OpenSim.LSL"; }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig conf = config.Configs["TSU.CCIR.OpenSim"];

            m_enabled = (conf != null && conf.GetBoolean("Enabled", false));
            m_log.Info(m_enabled ? "Enabled" : "Disabled");
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;

            m_scriptModuleComms = scene.RequestModuleInterface<IScriptModuleComms>();

            if (m_scriptModuleComms == null)
            {
                m_log.Error("IScriptModuleComms could not be found, cannot add script functions");
                return;
            }

            m_scriptModuleComms.RegisterScriptInvocation(this, new string[4]{
                "tsuccirRezLinkset",
                "tsuccirEvenlyDistributeChildPrims",
                "tsuccirEvenlyDistributeChildPrimsInOtherObject",
                "tsuccirRezDuplicate"
            });
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region OSSL

        private void ScriptError(SceneObjectPart origin, string msg)
        {
            m_scene.SimChat(msg, ChatTypeEnum.DebugChannel, origin.AbsolutePosition, origin.Name, origin.UUID, false);
        }

        public UUID tsuccirRezLinkset(UUID host, UUID script, int numberOfChildPrims)
        {
            numberOfChildPrims = Math.Max(0, numberOfChildPrims);

            SceneObjectPart part = m_scene.GetSceneObjectPart(host);

            if (part == null || !m_scene.Permissions.CanRezObject(1 + numberOfChildPrims, part.OwnerID, Vector3.Zero))
                return UUID.Zero;

            SceneObjectGroup newGroup = new SceneObjectGroup(part.OwnerID, part.AbsolutePosition, Quaternion.Identity, PrimitiveBaseShape.Default);
            for (int i = 0; i < numberOfChildPrims; ++i)
            {
                newGroup.AddPart(new SceneObjectPart(part.OwnerID, PrimitiveBaseShape.Default, part.AbsolutePosition, Quaternion.Identity, Vector3.Zero));
            }

            if (!m_scene.SceneGraph.AddNewSceneObject(newGroup, true, part.AbsolutePosition, Quaternion.Identity, Vector3.Zero))
            {
                ScriptError(part, "Failed to add linkset to scene graph.");
                return UUID.Zero;
            }
            else
            {
                newGroup.FromPartID = host;
                newGroup.ScheduleGroupForFullUpdate();
                IScriptModule[] modules = m_scene.RequestModuleInterfaces<IScriptModule>();
                foreach (IScriptModule module in modules)
                {
                    module.PostObjectEvent(host, "object_rez", new object[] { new LSL_String(newGroup.UUID.ToString()) });
                }
                return newGroup.UUID;
            }
        }

        #region evenly distribute child prims

        private bool EvenlyDistributeChildPrims(SceneObjectGroup group, Vector3 size, Vector3 point, Quaternion rot, Vector3 distribution, Vector3 margin)
        {

            int width = (int)distribution.X;
            int depth = (int)distribution.Y;
            int height = (int)distribution.Z;
            int volume = width * height * depth;

            bool distributionIsZeroVector = (Vector3)distribution == Vector3.Zero;

            if (!distributionIsZeroVector && (width < 1 || depth < 1 || height < 1))
            {
                ScriptError(group.RootPart, "Cannot evenly distribute prims across a distribution volume with axis less than 1");
                return false;
            }

            PhysicsActor pa = group.RootPart.PhysActor;
            if (pa != null && pa.IsPhysical)
            {
                size.X = Math.Max(group.Scene.m_minPhys, Math.Min(group.Scene.m_maxPhys, size.X));
                size.Y = Math.Max(group.Scene.m_minPhys, Math.Min(group.Scene.m_maxPhys, size.Y));
                size.Z = Math.Max(group.Scene.m_minPhys, Math.Min(group.Scene.m_maxPhys, size.Z));
            }
            else
            {
                // If not physical, then we clamp the scale to the non-physical min/max
                size.X = Math.Max(group.Scene.m_minNonphys, Math.Min(group.Scene.m_maxNonphys, size.X));
                size.Y = Math.Max(group.Scene.m_minNonphys, Math.Min(group.Scene.m_maxNonphys, size.Y));
                size.Z = Math.Max(group.Scene.m_minNonphys, Math.Min(group.Scene.m_maxNonphys, size.Z));
            }

            List<SceneObjectPart> parts = LSL_Api.GetLinkParts(group.RootPart, ScriptBaseClass.LINK_ALL_CHILDREN);
            if (volume == 0 && distributionIsZeroVector)
            {
                foreach (SceneObjectPart part in parts)
                {
                    part.OffsetPosition = point;
                    part.RotationOffset = rot;
                    part.Scale = size;
                }
                group.SendGroupFullUpdate();
                return true;
            }
            else if (volume < 2)
            {
                ScriptError(group.RootPart, "Cannot evenly distribute prims across a distribution volume less than 2");
                return false;
            }

            if (parts.Count > volume)
            {
                ScriptError(group.RootPart, "Cannot evenly distribute prims, distribution volume higher than specified number of prims to distribute");
                return false;
            }

            Vector3 start = new Vector3();
            start.X = ((size.X + margin.X) * (width - 1)) / -2.0f;
            start.Y = ((size.Y + margin.Y) * (depth - 1)) / -2.0f;
            start.Z = ((size.Z + margin.Z) * (height - 1)) / -2.0f;

            uint w = 0;
            uint d = 0;
            uint h = 0;

            foreach (SceneObjectPart part in parts)
            {
                float x = (w * (size.X + margin.X));
                float y = (d * (size.Y + margin.Y));
                float z = (h * (size.Z + margin.Z));
                part.OffsetPosition = (start + new Vector3(x, y, z)) * rot;
                part.OffsetPosition += point;
                part.RotationOffset = rot;
                part.Scale = size;

                if (++w >= width)
                {
                    w = 0;
                    ++d;
                }
                if (d >= depth)
                {
                    d = 0;
                    ++h;
                }
                if (h >= height)
                {
                    break;
                }
            }

            group.SendGroupFullUpdate();

            return true;
        }

        public int tsuccirEvenlyDistributeChildPrims(UUID host, UUID script, LSL_List args)
        {
            SceneObjectPart hostPart = m_scene.GetSceneObjectPart(host);
            hostPart.AddScriptLPS(1);

            LSL_Vector size = args.GetVector3Item(0);
            LSL_Vector point = args.GetVector3Item(1);
            LSL_Rotation rot = args.GetQuaternionItem(2);
            LSL_Vector distribution = args.GetVector3Item(3);
            LSL_Vector margin = args.GetVector3Item(4);

            return EvenlyDistributeChildPrims(hostPart.ParentGroup, size, point, rot, distribution, margin) ? 1 : 0;
        }

        public int tsuccirEvenlyDistributeChildPrimsInOtherObject(UUID host, UUID script, string otherObject, object[] argsObj)
        {
            SceneObjectPart hostPart = m_scene.GetSceneObjectPart(host);
            hostPart.AddScriptLPS(1);

            LSL_List args = new LSL_List();
            args.Data = argsObj;
            LSL_Vector size;
            LSL_Vector point;
            LSL_Rotation rot;
            LSL_Vector distribution;
            LSL_Vector margin;
            try
            {
                size = args.GetVector3Item(0);
                point = args.GetVector3Item(1);
                rot = args.GetQuaternionItem(2);
                distribution = args.GetVector3Item(3);
                margin = args.GetVector3Item(4);
            }
            catch (Exception e)
            {
                ScriptError(hostPart, e.Message);
                return 0;
            }

            UUID otherObjectKey;
            if (!UUID.TryParse(otherObject, out otherObjectKey))
            {
                ScriptError(hostPart, "Other object key is not a valid UUID");
                return 0;
            }

            SceneObjectPart other = hostPart.ParentGroup.Scene.GetSceneObjectPart(otherObjectKey);
            if (other == null)
            {
                ScriptError(hostPart, "Other object could not be found");
                return 0;
            }

            return EvenlyDistributeChildPrims(other.ParentGroup, size, point, rot, distribution, margin) ? 1 : 0;
        }

        #endregion

        public int tsuccirRezDuplicate(UUID host, UUID script, Vector3 offset, Quaternion rot)
        {
            SceneObjectPart hostPart = m_scene.GetSceneObjectPart(host);
            hostPart.AddScriptLPS(1);

            if (!m_scene.Permissions.CanRezObject(
                hostPart.ParentGroup.PrimCount,
                hostPart.OwnerID,
                hostPart.ParentGroup.AbsolutePosition + (Vector3)offset
            ))
            {
                ScriptError(hostPart, "Cannot duplicate object to destination, owner cannot rez objects at destination parcel.");

                System.Threading.Thread.Sleep(100);
            }
            else
            {
                SceneObjectGroup duplicate = m_scene.SceneGraph.DuplicateObject(
                    hostPart.ParentGroup.LocalId,
                    offset,
                    hostPart.ParentGroup.RootPart.GetEffectiveObjectFlags(),
                    hostPart.OwnerID,
                    hostPart.GroupID,
                    rot
                );
                
                IScriptModule[] modules = m_scene.RequestModuleInterfaces<IScriptModule>();
                foreach (IScriptModule module in modules)
                {
                    module.PostObjectEvent(host, "object_rez", new object[] { new LSL_String(duplicate.RootPart.UUID.ToString()) });
                }

                System.Threading.Thread.Sleep(100);
                foreach (IScriptModule module in modules)
                {
                    module.PostObjectEvent(duplicate.UUID, "on_rez", new object[] { new LSL_Integer(0) });
                }
            }

            return 0;
        }

        #endregion
    }
}
