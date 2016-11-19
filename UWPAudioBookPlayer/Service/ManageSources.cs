using System;
using System.Collections.Generic;
using System.Linq;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.Service
{
    public class ManageSources
    {
        public ICloudController[] GetControllersForDownload(AudioBookSourceWithClouds book,
            ICloudController[] controllers)
        {
            return GetSupportedControllers(book, controllers);
        }

        public ICloudController[] GetControllersForUpload(AudioBookSourceWithClouds book,
            ICloudController[] controllers)
        {
            return GetSupportedControllers(book, controllers).Where(x => x.IsCloud).ToArray();
        }

        private ICloudController[] GetSupportedControllers(AudioBookSourceWithClouds book,
            ICloudController[] controllers)
        {
            var list =
                controllers?.Where(
                    x =>
                        book.AdditionSources?.OfType<AudioBookSourceCloud>().Any(b =>
                            String.Equals(b.CloudStamp, x.CloudStamp, StringComparison.OrdinalIgnoreCase)) ??
                        false)?.ToList() ?? new List<ICloudController>();
            var cntl = controllers.FirstOrDefault(controller =>
                String.Equals(controller.CloudStamp, (book as AudioBookSourceCloud)?.CloudStamp,
                    StringComparison.OrdinalIgnoreCase));
            list.Add(cntl);

            return list.Where(x => x != null).ToArray();
        }
    }
}
