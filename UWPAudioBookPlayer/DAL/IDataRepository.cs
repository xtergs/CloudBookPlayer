using System.Threading.Tasks;
using UWPAudioBookPlayer.DAL.Model;
using UWPAudioBookPlayer.Model;

namespace UWPAudioBookPlayer.DAL
{
    public interface IDataRepository
    {
        string FileName { get; set; }

        Task<SaveModel> Load();
        Task Save(SaveModel books);

        BookMark[] BookMarks(AudioBookSourceWithClouds book);
        bool AddBookMark(AudioBookSourceWithClouds book, BookMark bookMark);
    }
}