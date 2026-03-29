using System;
using Newtonsoft.Json.Linq;
using PocketBaseSdk;
using TMPro;
using UnityEngine;

public class PocketBaseExamples : MonoBehaviour
{
    [SerializeField] private TextAsset _config;
    [SerializeField] private TMP_InputField _otpCode;

    private string _pocketBaseUrl;
    private string _email;
    private string _password;

    private PocketBase _pb;
    private string _otpId;

    private void Awake()
    {
        var config = JObject.Parse(_config.text);
        _email = config["email"]?.ToString();
        _password = config["password"]?.ToString();
        _pocketBaseUrl = config["pocketBaseUrl"]?.ToString();

        _pb = new PocketBase(_pocketBaseUrl, authStore: AsyncAuthStore.PlayerPrefs);
    }

    [ContextMenu(nameof(ConnectToCollectionSSE))]
    private async void ConnectToCollectionSSE()
    {
        await _pb.Collection("example").Subscribe("*", e =>
        {
            Debug.Log(e.Action);
            Debug.Log(e.Record);
        });
    }

    [ContextMenu(nameof(DisconnectFromCollectionSSE))]
    private async void DisconnectFromCollectionSSE()
    {
        await _pb.Collection("example").Unsubscribe("*");
    }
    
    [ContextMenu(nameof(HealthCheck))]
    public async void HealthCheck()
    {
        try
        {
            var result = await _pb.Health.Check();
            Debug.Log(result);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    [ContextMenu(nameof(ListAuthMethods))]
    public async void ListAuthMethods()
    {
        try
        {
            AuthMethodsList authMethods = await _pb.Collection("users").ListAuthMethods();
            Debug.Log(authMethods);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [ContextMenu(nameof(RequestOTP))]
    public async void RequestOTP()
    {
        try
        {
            OTPResponse response = await _pb.Collection("users").RequestOTP(_email);
            _otpId = response.OtpId;

            Debug.Log(response);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [ContextMenu(nameof(AuthWithOTP))]
    public async void AuthWithOTP()
    {
        try
        {
            RecordAuth auth = await _pb.Collection("users").AuthWithOTP(
                otpId: _otpId,
                password: _otpCode.text
            );

            Debug.Log(auth);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    [ContextMenu(nameof(LoginWithGoogleAsync))]
    public async void LoginWithGoogleAsync()
    {
        try
        {
            // Start the OAuth2 flow
            RecordAuth auth = await _pb.Collection("users").AuthWithOAuth2("google", url =>
            {
                // Launch the OAuth2 URL in a web view or browser.
                // This is where the user will log in with the provider.
                Application.OpenURL(url.AbsoluteUri);
            });

            Debug.Log($"Success! {auth.Record}");
        }
        catch (ClientException e)
        {
            Debug.LogError(e.OriginalError);
        }
    }

    [ContextMenu(nameof(Login))]
    private async void Login()
    {
        try
        {
            RecordAuth result = await _pb.Collection("users").AuthWithPassword(_email, _password);
            Debug.Log(result.Record);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    [ContextMenu(nameof(GetFullList))]
    private async void GetFullList()
    {
        try
        {
            var result = await _pb.Collection("Posts").GetFullList(10);

            foreach (var post in result)
            {
                Debug.Log(post);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    [ContextMenu(nameof(GetOne))]
    private async void GetOne()
    {
        try
        {
            var result = await _pb.Collection("Posts").GetOne("3g28fsld3arvpaz");
            
            Post post = Post.FromRecord(result);
            var status = (int)result["status"];
            var nested = result["expand"]?["user"]?.ToObject<RecordModel>();
            var nested2 = result["expand"]?["user"]?["title"]?.ToString();
            Debug.Log(post.Title);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    [ContextMenu(nameof(GetOneSimple))]
    private async void GetOneSimple()
    {
        try
        {
            var record = await _pb.Collection("examples").GetOne("hz4l808h4w32713", expand: "other");
            Debug.Log(record.Id);
            Debug.Log(record.CollectionId);
            Debug.Log(record.CollectionName);
            Debug.Log(record.GetStringValue("title"));
            Debug.Log(record.GetStringValue("expand.other.title"));

            Debug.Log(record);
        }
        catch (ClientException e)
        {
            Debug.LogException(e);
        }
    }
}