using System.Collections;
using agora_gaming_rtc;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;

using static agora_gaming_rtc.ExternalVideoFrame;

// this is an example of using Agora Unity SDK
// It demonstrates:
// How to enable video
// How to join/leave channel
// How to share the external video source
// Where to obtain the video stream from the AR Camera 
public class TestUnityARClient : IVideoChatClient
{
    // instance of agora engine
    private IRtcEngine mRtcEngine;

    public static TextureFormat ConvertFormat = TextureFormat.BGRA32;
    public static VIDEO_PIXEL_FORMAT PixelFormat = VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_BGRA;

    ARCameraManager cameraManager;

    MonoBehaviour monoProxy;
    int i = 0; // monotonic timestamp counter

    // load agora engine
    public void loadEngine(string appId)
    {
        // start sdk
        Debug.Log("initializeEngine");

        if (mRtcEngine != null)
        {
            Debug.Log("Engine exists. Please unload it first!");
            return;
        }

        // init engine
        mRtcEngine = IRtcEngine.GetEngine(appId);

        // enable log
        mRtcEngine.SetLogFilter(LOG_FILTER.DEBUG | LOG_FILTER.INFO | LOG_FILTER.WARNING | LOG_FILTER.ERROR | LOG_FILTER.CRITICAL);
    }

    public void join(string channel)
    {
        Debug.Log("calling join (channel = " + channel + ")");

        if (mRtcEngine == null)
            return;

        // set callbacks (optional)
        mRtcEngine.OnJoinChannelSuccess = onJoinChannelSuccess;
        mRtcEngine.OnUserJoined = onUserJoined;
        mRtcEngine.OnUserOffline = onUserOffline;

        // enable video
        mRtcEngine.EnableVideo();
        // allow camera output callback
        mRtcEngine.EnableVideoObserver();
        //mRtcEngine.EnableLocalVideo(false);
        CameraCapturerConfiguration config = new CameraCapturerConfiguration();
        config.preference = CAPTURER_OUTPUT_PREFERENCE.CAPTURER_OUTPUT_PREFERENCE_AUTO;
        config.cameraDirection = CAMERA_DIRECTION.CAMERA_REAR;
        mRtcEngine.SetCameraCapturerConfiguration(config);

        mRtcEngine.SetExternalVideoSource(true, false);

        // join channel
        mRtcEngine.JoinChannel(channel, null, 0);

        // Optional: if a data stream is required, here is a good place to create it
        int streamID = mRtcEngine.CreateDataStream(true, true);
        Debug.Log("initializeEngine done, data stream id = " + streamID);
    }

    public void leave()
    {
        Debug.Log("calling leave");

        if (mRtcEngine == null)
            return;

        // leave channel
        mRtcEngine.LeaveChannel();
        // deregister video frame observers in native-c code
        mRtcEngine.DisableVideoObserver();
    }

    // unload agora engine
    public void unloadEngine()
    {
        Debug.Log("calling unloadEngine");

        // delete
        if (mRtcEngine != null)
        {
            IRtcEngine.Destroy();  // Place this call in ApplicationQuit
            mRtcEngine = null;
        }
    }


    public void EnableVideo(bool pauseVideo)
    {
        if (mRtcEngine != null)
        {
            if (!pauseVideo)
            {
                mRtcEngine.EnableVideo();
            }
            else
            {
                mRtcEngine.DisableVideo();
            }
        }
    }

    public void onSceneLoaded()
    {

    }

    // implement engine callbacks
    private void onJoinChannelSuccess(string channelName, uint uid, int elapsed)
    {
        Debug.Log("JoinChannelSuccessHandler: uid = " + uid);

        // get camera image
        GameObject go = GameObject.Find("AR Camera");
        if (go != null)
        {
            cameraManager = go.GetComponent<ARCameraManager>();
            monoProxy = go.GetComponent<MonoBehaviour>();
        }

    }

    // When a remote user joined, this delegate will be called. Typically
    // create a GameObject to render video on it
    private void onUserJoined(uint uid, int elapsed)
    {
        Debug.Log("onUserJoined: uid = " + uid);
        // this is called in main thread

        // find a game object to render video stream from 'uid'
        GameObject go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            return; // reuse
        }

        // create a GameObject and assigne to this new user
        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!ReferenceEquals(go, null))
        {
            go.name = uid.ToString();

            // configure videoSurface
            VideoSurface o = go.AddComponent<VideoSurface>();
            o.SetForUser(uid);
            o.SetEnable(true);
            o.transform.Rotate(-90.0f, 0.0f, 0.0f);
            o.transform.position = getNextCubePosition();
            o.transform.localScale = Vector3.one;
        }
        OnEnable();
    }

    void OnEnable()
    {
        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }


    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        CaptureARBuffer();
    }


    private static int totalUsers = 0;

    // Generate a new position to place the new cube in.  
    Vector3 getNextCubePosition()
    {
        totalUsers += 1;
        GameObject go = GameObject.Find("Sphere");
        float x = 0;
        if (go != null)
        {
            x = go.transform.position.x + totalUsers * 1.5f;
        }
        else
        {
            x = UnityEngine.Random.Range(-2.0f, 2.0f);
        }

        return new Vector3(x, 0, 8.17f);
    }
    // When remote user is offline, this delegate will be called. Typically
    // delete the GameObject for this user
    private void onUserOffline(uint uid, USER_OFFLINE_REASON reason)
    {
        // remove video stream
        Debug.Log("onUserOffline: uid = " + uid + " reason = " + reason);
        // this is called in main thread
        GameObject go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            UnityEngine.Object.Destroy(go);
        }
        OnDisable();
    }

    // Get Image from the AR Camera, extract the raw data from the image 
    private unsafe void CaptureARBuffer()
    {
        // Get the image in the ARSubsystemManager.cameraFrameReceived callback

        XRCameraImage image;
        if (!cameraManager.TryGetLatestImage(out image))
        {
            Debug.LogWarning("Capture AR Buffer returns nothing!!!!!!");
            return;
        }

        var conversionParams = new XRCameraImageConversionParams
        {
            // Get the full image
            inputRect = new RectInt(0, 0, image.width, image.height),

            // Downsample by 2
            outputDimensions = new Vector2Int(image.width, image.height),

            // Color image format
            outputFormat = ConvertFormat,

            // Flip across the x axis
            transformation = CameraImageTransformation.MirrorX

            // Call ProcessImage when the async operation completes
        };
        // See how many bytes we need to store the final image.
        int size = image.GetConvertedDataSize(conversionParams);

        Debug.Log("OnCameraFrameReceived, size == " + size + "w:" + image.width + " h:" + image.height + " planes=" + image.planeCount);


        // Allocate a buffer to store the image
        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        // Extract the image data
        image.Convert(conversionParams, new System.IntPtr(buffer.GetUnsafePtr()), buffer.Length);

        // The image was converted to RGBA32 format and written into the provided buffer
        // so we can dispose of the CameraImage. We must do this or it will leak resources.

        byte[] bytes = buffer.ToArray();
        monoProxy.StartCoroutine(PushFrame(bytes, image.width, image.height,
                 () => { image.Dispose(); buffer.Dispose(); }));
    }

    // Push frame to the remote client
    IEnumerator PushFrame(byte[] bytes, int width, int height, System.Action onFinish)
    {
        if (bytes == null || bytes.Length == 0)
        {
            Debug.LogError("Zero bytes found!!!!");
            yield break;
        }

        IRtcEngine rtc = IRtcEngine.QueryEngine();
        //if the engine is present
        if (rtc != null)
        {
            //Create a new external video frame
            ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
            //Set the buffer type of the video frame
            externalVideoFrame.type = ExternalVideoFrame.VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
            // Set the video pixel format
            //externalVideoFrame.format = ExternalVideoFrame.VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_BGRA;
            externalVideoFrame.format = PixelFormat;
            //apply raw data you are pulling from the rectangle you created earlier to the video frame
            externalVideoFrame.buffer = bytes;
            //Set the width of the video frame (in pixels)
            externalVideoFrame.stride = width;
            //Set the height of the video frame
            externalVideoFrame.height = height;
            //Remove pixels from the sides of the frame
            externalVideoFrame.cropLeft = 10;
            externalVideoFrame.cropTop = 10;
            externalVideoFrame.cropRight = 10;
            externalVideoFrame.cropBottom = 10;
            //Rotate the video frame (0, 90, 180, or 270)
            externalVideoFrame.rotation = 90;
            // increment i with the video timestamp
            externalVideoFrame.timestamp = i++;
            //Push the external video frame with the frame we just created
            int a = rtc.PushVideoFrame(externalVideoFrame);
            Debug.Log(" pushVideoFrame(" + i + ") size:" + bytes.Length + " => " + a);

        }
        yield return null;
        onFinish();
    }
}
