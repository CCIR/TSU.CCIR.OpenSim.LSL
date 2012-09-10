using System.Collections.Generic;
using System.ComponentModel;

using OpenSim.Region.ScriptEngine.Shared;

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
            foreach(object o in list)
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
    }
}
