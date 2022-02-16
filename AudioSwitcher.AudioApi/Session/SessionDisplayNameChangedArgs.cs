namespace AudioSwitcher.AudioApi.Session
{
    public class SessionDisplayNameChangedArgs
    {
        public IAudioSession Session { get; private set; }

        public string DisplayName { get; private set; }

        public SessionDisplayNameChangedArgs(IAudioSession session, string displayName)
        {
            Session = session;
            DisplayName = displayName;
        }
    }
}
