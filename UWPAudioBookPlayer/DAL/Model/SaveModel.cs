using SQLite.Net.Attributes;
using UWPAudioBookPlayer.Model;
using UWPAudioBookPlayer.ModelView;

namespace UWPAudioBookPlayer.DAL.Model
{
    public class CloudService
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Token { get; set; }
        public string CloudStamp { get; set; }
    }
    public class SaveModel
    {
        public CurrentState CurrentState { get; set; }
        public AudioBookSourceWithClouds[] AudioBooks { get; set; } = new AudioBookSourceWithClouds[0];
        public OnlineAudioBookSource[] OnlineBooks { get; set; } = new OnlineAudioBookSource[0];
        public CloudService[] CloudServices { get; set; } = new CloudService[0];
        public Folder BaseFolder { get; set; }
    }
}
