using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Controls;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.Service;

namespace UWPAudioBookPlayer.View
{
	public static class ViewHelper
	{
		private struct CacheEntry
		{
			public CacheEntry(BitmapImage image)
			{
				Image = image;
				LastAccessUtc = DateTime.UtcNow;
			}
			private BitmapImage Image { get; set; }	
			public DateTime LastAccessUtc { get; private set; }

			public BitmapImage Get()
			{
				LastAccessUtc = DateTime.UtcNow;
				if (Image == null)
					return new BitmapImage();
				return Image;
			}
		}
		private static Dictionary<string, CacheEntry> fileImageCache = new Dictionary<string, CacheEntry>();
		private static void SetDefaultImage(ImageEx img)
		{
			CacheEntry btm;
			if (fileImageCache.TryGetValue("default", out btm))
			{
				img.Source = btm.Get();
				return;
			}
			if (string.IsNullOrWhiteSpace(StandartCover))
				return;
			btm = new CacheEntry(new BitmapImage(new Uri(StandartCover)));
			fileImageCache["defualt"] = btm;
			img.Source = btm.Get();
		}

		public static string StandartCover { get; set; }

		public static Task<Stream> DownloadFileFromBook(AudioBookSource source, string file, ControllersService serivice)
		{
			var controller = serivice.GetContorller(source);
			if (controller == null)
				return Task.FromResult(default(Stream));
			return controller.DownloadBookFile(source.Name, file);
		}

		public static async Task LoadImage(ImageEx img, AudioBookSourceWithClouds source, ControllersService service)
		{
			ImageStruct cover = source.Cover;
			if (!cover.IsValide)
			{
				var mediaFile = await source.GetFile(source.GetCurrentFile.Name);
				if (mediaFile != null)
					using (StorageItemThumbnail thumbnail = await mediaFile.GetThumbnailAsync(ThumbnailMode.MusicView, 1080))
					{
						if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
						{
							var bitmapImage = new BitmapImage();
							bitmapImage.SetSource(thumbnail);
							img.Source = bitmapImage;
						}
						else
							SetDefaultImage(img);
					}
				else
					SetDefaultImage(img);
				return;
			}
			if (source.IsLink(cover.Url))
			{
				try
				{
					var Bitmap =
						await ImageCache.Instance.GetFromCacheAsync(new Uri(cover.Url, UriKind.Absolute), Guid.NewGuid().ToString(), true);
					img.Source = Bitmap;
					return;
				}
				catch (Exception e)
				{
					SetDefaultImage(img);
				}
			}
			string cachekey = source.Name + cover;
			try
			{
				CacheEntry btmimage;
				if (fileImageCache.TryGetValue(cachekey, out btmimage))
				{
					img.Source = btmimage;
					return;
				}
				var streamResult = await source.GetFileStream(cover.Url);
				btmimage = new CacheEntry();
				streamResult.Item2.Seek(0);
				await btmimage.Get().SetSourceAsync(streamResult.Item2);
				streamResult.Item2.Dispose();
				fileImageCache[cachekey] = btmimage;
				img.Source = btmimage;
				streamResult = null;
			}
			catch (Exception e)
			{
				try
				{
					using (var stream = await DownloadFileFromBook(source, source.Cover.Title, service))
					{
						if (stream == null)
							return;
						var bitmap = new CacheEntry();
						await bitmap.Get().SetSourceAsync(stream.AsRandomAccessStream());
						fileImageCache[cachekey] = bitmap;
						img.Source = bitmap;
					}

				}
				catch (Exception ex)
				{
					//no way to load cover
				}
			}
		}
	}
}
