using System.Threading.Tasks;
using UWPAudioBookPlayer.DAL.Model;

namespace UWPAudioBookPlayer.DAL
{
    public interface IDataRepository
    {
        string FileName { get; set; }

        Task<SaveModel> Load();
        Task Save(SaveModel books);
    }
}