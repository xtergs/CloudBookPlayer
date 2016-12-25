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
            if (book is AudioBookSourceCloud)
                return new ICloudController[0];
            return controllers.Where(x=> x!= null && x.IsCloud).ToArray();
        }

        private ICloudController[] GetSupportedControllers(AudioBookSourceWithClouds book,
            ICloudController[] controllers)
        {
            AudioBookSourceCloud loudData = book as AudioBookSourceCloud;
            if (loudData == null)
                return new ICloudController[0];
            var llist = new[] { loudData }.Union(book.AdditionSources?.OfType<AudioBookSourceCloud>());
            ICloudController[] louds =
                controllers.Join(llist, controller => controller.CloudStamp, cloud => cloud.CloudStamp,
                    (controller, cloud) => controller).ToArray();

            return louds;
        }
    }
}
