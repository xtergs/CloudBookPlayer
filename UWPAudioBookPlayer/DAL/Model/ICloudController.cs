﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL.Model
{
    public interface ICloudController
    {
        string BaseFolder { get; set; }
        bool IsAutorized { get; }
        string Token { get; set; }

        event EventHandler CloseAuthPage;
        event EventHandler<Tuple<Uri, Action<Uri>>> NavigateToAuthPage;

        void Auth();
        Task DeleteAudioBook(AudioBookSource source);
        Task<Stream> DownloadBookFile(string BookName, string fileName);
        Task<AudioBookSourceCloud> GetAudioBookInfo(string bookName);
        Task<AudioBookSourceCloud> GetAudioBookInfo(AudioBookSource book);
        Task<List<AudioBookSourceCloud>> GetAudioBooksInfo();
        Task<ulong> GetFreeSpaceBytes();
        Task<string> GetLink(string bookName, string fileName);
        Task<string> GetLink(AudioBookSourceCloud book, int fileNumber);
        void Inicialize();
        Task Uploadbook(string BookName, string fileName, Stream stream);
        Task UploadBookMetadata(AudioBookSource source, string revision = null);
    }
}