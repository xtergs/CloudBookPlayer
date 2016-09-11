using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace UWPAudioBookPlayer.Service
{
    public class UniversalApplicationSettingsHelper : IApplicationSettingsHelper
    {
        readonly Dictionary<string, DateTime> dateTimeDictionary = new Dictionary<string, DateTime>();

        public DateTime SimbleGet(DateTime value = default(DateTime), [CallerMemberName] string key = null)
        {
            if (dateTimeDictionary.ContainsKey(key))
            {
                if (dateTimeDictionary[key] != default(DateTime))
                    return dateTimeDictionary[key];
            }
            else
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                {
                    var backField =
                        DateTime.Parse(ApplicationData.Current.LocalSettings.Values[key].ToString());
                    dateTimeDictionary.Add(key, backField);
                    return backField;
                }

            }
            return value;
        }

        public void SimpleSet<T>(T value, [CallerMemberName] string key = null)
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                ApplicationData.Current.LocalSettings.Values.Add(key, value);
            else
                ApplicationData.Current.LocalSettings.Values[key] = value;
        }

        public T SimpleGet<T>(T defValue = default(T), [CallerMemberName] string key = null)
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                ApplicationData.Current.LocalSettings.Values.Add(key, defValue);
            return (T)ApplicationData.Current.LocalSettings.Values[key];
        }

        public void SimpleSet(DateTime value, [CallerMemberName] string key = null)
        {
            if (dateTimeDictionary.ContainsKey(key) && dateTimeDictionary[key] == value)
                return;
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
            {
                ApplicationData.Current.LocalSettings.Values.Add(key, value.ToString());
                dateTimeDictionary.Add(key, value);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[key] = value.ToString();
                dateTimeDictionary[key] = value;
            }
        }

        
    }
}
