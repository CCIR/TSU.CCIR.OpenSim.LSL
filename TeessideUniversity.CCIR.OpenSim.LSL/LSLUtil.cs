using System.Collections.Generic;
using System.ComponentModel;

using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;

namespace TeessideUniversity.CCIR.OpenSim
{
    public static class LSLUtil
    {
        public static List<T> TypedList<T>(LSL_Types.list list, T defaultValue)
        {
            return TypedList<T>(list.Data, defaultValue);
        }

        public static List<T> TypedList<T>(object[] list, T defaultValue)
        {
            List<T> resp = new List<T>(list.Length);
            foreach (object o in list)
            {
                try
                {
                    resp.Add((T)TypeDescriptor.GetConverter(
                            typeof(T)).ConvertFrom(o));
                }
                catch
                {
                    resp.Add(defaultValue);
                }
            }

            return resp;
        }

        public static List<int> AttachmentList(LSL_Types.list list)
        {
            return AttachPoints(list.Data);
        }

        public static List<int> AttachPoints(object[] list)
        {
            List<int> resp = new List<int>();
            foreach (object o in list)
            {
                try
                {
                    resp.Add((int)TypeDescriptor.GetConverter(
                            typeof(int)).ConvertFrom(o));
                }
                catch { }
            }

            resp.RemoveAll(point =>
            {
                switch (point)
                {
                    case ScriptBaseClass.ATTACH_CHEST:
                    case ScriptBaseClass.ATTACH_HEAD:
                    case ScriptBaseClass.ATTACH_LSHOULDER:
                    case ScriptBaseClass.ATTACH_RSHOULDER:
                    case ScriptBaseClass.ATTACH_LHAND:
                    case ScriptBaseClass.ATTACH_RHAND:
                    case ScriptBaseClass.ATTACH_LFOOT:
                    case ScriptBaseClass.ATTACH_RFOOT:
                    case ScriptBaseClass.ATTACH_BACK:
                    case ScriptBaseClass.ATTACH_PELVIS:
                    case ScriptBaseClass.ATTACH_MOUTH:
                    case ScriptBaseClass.ATTACH_CHIN:
                    case ScriptBaseClass.ATTACH_LEAR:
                    case ScriptBaseClass.ATTACH_REAR:
                    case ScriptBaseClass.ATTACH_LEYE:
                    case ScriptBaseClass.ATTACH_REYE:
                    case ScriptBaseClass.ATTACH_NOSE:
                    case ScriptBaseClass.ATTACH_RUARM:
                    case ScriptBaseClass.ATTACH_RLARM:
                    case ScriptBaseClass.ATTACH_LUARM:
                    case ScriptBaseClass.ATTACH_LLARM:
                    case ScriptBaseClass.ATTACH_RHIP:
                    case ScriptBaseClass.ATTACH_RULEG:
                    case ScriptBaseClass.ATTACH_RLLEG:
                    case ScriptBaseClass.ATTACH_LHIP:
                    case ScriptBaseClass.ATTACH_LULEG:
                    case ScriptBaseClass.ATTACH_LLLEG:
                    case ScriptBaseClass.ATTACH_BELLY:
                    case ScriptBaseClass.ATTACH_LEFT_PEC:
                    case ScriptBaseClass.ATTACH_RIGHT_PEC:
                        return false;
                    default:
                        return true;
                }
            });

            return resp;
        }
    }
}
