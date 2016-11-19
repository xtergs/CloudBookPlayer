using System.Threading.Tasks;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL
{
    public interface IDataRepository
    {
        string LocalFileName { get; set; }

        Task<SaveModel> Load();
        Task Save(SaveModel books);

        BookMark[] BookMarks(AudioBookSourceWithClouds book);
        bool AddBookMark(AudioBookSourceWithClouds book, BookMark bookMark);
        void UpdateBookMark(AudioBookSourceWithClouds playingSource, BookMark obj);
        bool RemoveBookMark(BookMark bookMark, AudioBookSourceWithClouds audioBook);
    }
}