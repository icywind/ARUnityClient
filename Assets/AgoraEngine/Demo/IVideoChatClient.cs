public interface IVideoChatClient
{
    void join(string channel);
    void leave();
    void loadEngine(string appId);
    void unloadEngine();
    void onSceneLoaded();
    void EnableVideo(bool enable);
}
