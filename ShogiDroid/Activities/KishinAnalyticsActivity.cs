using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Uri = Android.Net.Uri;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.Interop;

namespace ShogiDroid;

[Activity(Label = "盤面読み取り", Theme = "@style/Theme.AppCompat.Light")]
public class KishinAnalyticsActivity : Activity
{
	private const string KISHIN_URL = "https://kishin-analytics.heroz.jp/";
	private const int CAMERA_PERMISSION_CODE = 1001;
	private const int FILE_CHOOSER_CODE = 1002;

	private WebView webView;
	private ProgressBar progressBar;
	private View overlay;
	private IValueCallback fileUploadCallback;
	private Android.Net.Uri cameraImageUri;
	private PermissionRequest pendingPermissionRequest;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		SetContentView(Resource.Layout.kishin_analytics);

		webView = FindViewById<WebView>(Resource.Id.kishin_webview);
		progressBar = FindViewById<ProgressBar>(Resource.Id.kishin_progress_bar);
		overlay = FindViewById<View>(Resource.Id.kishin_overlay);

		FindViewById<ImageButton>(Resource.Id.kishin_close_button).Click += (s, e) =>
		{
			SetResult(Result.Canceled);
			Finish();
		};

		// Request camera permission upfront
		if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) != Permission.Granted)
		{
			ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.Camera }, CAMERA_PERMISSION_CODE);
		}

		SetupWebView();
		SetupCookies();
		webView.LoadUrl(KISHIN_URL);
	}

	private void SetupWebView()
	{
		WebSettings settings = webView.Settings;
		settings.JavaScriptEnabled = true;
		settings.DomStorageEnabled = true;
		settings.AllowFileAccess = true;
		settings.AllowContentAccess = true;
		settings.MediaPlaybackRequiresUserGesture = false;

		// UserAgent: use Chrome-like UA to avoid WebView detection blocks
		string defaultUA = settings.UserAgentString;
		if (defaultUA.Contains("wv"))
		{
			settings.UserAgentString = defaultUA.Replace("; wv", "");
		}

		webView.SetWebViewClient(new KishinWebViewClient(this));
		webView.SetWebChromeClient(new KishinWebChromeClient(this));
		webView.AddJavascriptInterface(new ShogiDroidJsInterface(this), "ShogiDroid");
	}

	private void SetupCookies()
	{
		CookieManager cookieManager = CookieManager.Instance;
		cookieManager.SetAcceptCookie(true);
		cookieManager.SetAcceptThirdPartyCookies(webView, true);
	}

	protected override void OnPause()
	{
		base.OnPause();
		CookieManager.Instance.Flush();
	}

	public override void OnBackPressed()
	{
		if (webView.CanGoBack())
		{
			webView.GoBack();
		}
		else
		{
			SetResult(Result.Canceled);
			base.OnBackPressed();
		}
	}

	protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
	{
		if (requestCode == FILE_CHOOSER_CODE)
		{
			if (fileUploadCallback == null)
				return;

			Uri[] results = null;
			if (resultCode == Result.Ok)
			{
				if (data != null && data.Data != null)
				{
					results = new Uri[] { data.Data };
				}
				else if (cameraImageUri != null)
				{
					results = new Uri[] { cameraImageUri };
				}
			}

			fileUploadCallback.OnReceiveValue(results != null
				? Java.Lang.Object.FromArray(results)
				: null);
			fileUploadCallback = null;
			cameraImageUri = null;
			return;
		}
		base.OnActivityResult(requestCode, resultCode, data);
	}

	public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
	{
		base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		if (requestCode == CAMERA_PERMISSION_CODE && pendingPermissionRequest != null)
		{
			if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
			{
				pendingPermissionRequest.Grant(pendingPermissionRequest.GetResources());
			}
			else
			{
				pendingPermissionRequest.Deny();
			}
			pendingPermissionRequest = null;
		}
	}

	private void OnSfenExtracted(string sfen)
	{
		RunOnUiThread(() =>
		{
			// Show overlay immediately to hide the Kishin Analytics page
			overlay.Visibility = ViewStates.Visible;
			Intent resultIntent = new Intent();
			resultIntent.PutExtra("sfen", sfen);
			SetResult(Result.Ok, resultIntent);
			Finish();
		});
	}

	private void ShowOverlay()
	{
		RunOnUiThread(() => overlay.Visibility = ViewStates.Visible);
	}

	private void HideOverlay()
	{
		RunOnUiThread(() => overlay.Visibility = ViewStates.Gone);
	}

	private string TryExtractSfenFromUrl(string url)
	{
		if (string.IsNullOrEmpty(url))
			return null;

		try
		{
			Uri uri = Uri.Parse(url);

			// Check for sfen parameter
			string sfen = uri.GetQueryParameter("sfen");
			if (!string.IsNullOrEmpty(sfen))
				return sfen;

			// Check for bod/position parameter
			string position = uri.GetQueryParameter("position");
			if (!string.IsNullOrEmpty(position))
				return position;

			// Check URL path for SFEN-like pattern (contains piece placement with slashes)
			string path = uri.Path;
			if (path != null && path.Contains("/") && IsSfenLike(path))
				return ExtractSfenFromPath(path);
		}
		catch
		{
			// Ignore URL parse errors
		}

		return null;
	}

	private static bool IsSfenLike(string text)
	{
		// SFEN board part contains piece chars and slashes like "lnsgkgsnl/..."
		int slashCount = 0;
		foreach (char c in text)
		{
			if (c == '/') slashCount++;
		}
		return slashCount >= 8;
	}

	private static string ExtractSfenFromPath(string path)
	{
		// Try to find SFEN-like substring in the path
		string[] parts = path.Split('/');
		foreach (string part in parts)
		{
			if (IsSfenLike(part))
				return System.Net.WebUtility.UrlDecode(part);
		}
		return null;
	}

	private bool autoNavigated;

	private void TryAutoNavigateToScanner()
	{
		if (autoNavigated)
			return;

		// Wait for the React SPA to render the camera button, click it,
		// then wait for scanner UI to appear before revealing the WebView
		string js = @"
(function() {
  if (window._shogiDroidAutoNav) return;
  window._shogiDroidAutoNav = true;
  var attempts = 0;
  var timer = setInterval(function() {
    var btn = document.querySelector('button[aria-label=""camera""]');
    if (btn) {
      clearInterval(timer);
      btn.click();
      // Wait for scanner UI to render, then reveal
      var scanAttempts = 0;
      var scanTimer = setInterval(function() {
        var video = document.querySelector('video');
        if (video) {
          clearInterval(scanTimer);
          ShogiDroid.onScannerReady();
        } else if (++scanAttempts > 50) {
          clearInterval(scanTimer);
          ShogiDroid.onScannerReady();
        }
      }, 100);
    } else if (++attempts > 30) {
      clearInterval(timer);
      // Camera button not found (not logged in?) - show page for login
      ShogiDroid.onScannerReady();
    }
  }, 200);
})();
";
		webView.EvaluateJavascript(js, null);
		autoNavigated = true;
	}

	private void InjectSfenObserver()
	{
		string js = @"
(function() {
  if (window._shogiDroidObserverInstalled) return;
  window._shogiDroidObserverInstalled = true;

  // Pattern B: Observe DOM for SFEN data attributes or known elements
  var observer = new MutationObserver(function(mutations) {
    // Check for data-sfen attribute
    var el = document.querySelector('[data-sfen]');
    if (el) {
      var sfen = el.getAttribute('data-sfen');
      if (sfen && sfen.length > 10) {
        ShogiDroid.receiveSfen(sfen);
        return;
      }
    }

    // Check for SFEN in textarea/input
    var inputs = document.querySelectorAll('input[type=text], textarea');
    for (var i = 0; i < inputs.length; i++) {
      var val = inputs[i].value;
      if (val && /^(sfen\s+)?[1-9lnsgkrpbLNSGKRPB+]+\//.test(val)) {
        ShogiDroid.receiveSfen(val.replace(/^sfen\s+/, ''));
        return;
      }
    }

    // Check for SFEN in URL hash
    if (location.hash && location.hash.length > 10) {
      var hash = decodeURIComponent(location.hash.substring(1));
      if (/^(sfen\s+)?[1-9lnsgkrpbLNSGKRPB+]+\//.test(hash)) {
        ShogiDroid.receiveSfen(hash.replace(/^sfen\s+/, ''));
        return;
      }
    }
  });
  observer.observe(document.body, {childList: true, subtree: true, attributes: true, characterData: true});

  // Pattern C: Hook fetch/XHR to intercept API responses containing SFEN
  var origFetch = window.fetch;
  if (origFetch) {
    window.fetch = function() {
      return origFetch.apply(this, arguments).then(function(response) {
        var cloned = response.clone();
        cloned.text().then(function(text) {
          try {
            var json = JSON.parse(text);
            var sfen = findSfenInObject(json);
            if (sfen) ShogiDroid.receiveSfen(sfen);
          } catch(e) {}
        }).catch(function(){});
        return response;
      });
    };
  }

  var origOpen = XMLHttpRequest.prototype.open;
  var origSend = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.open = function() {
    this._url = arguments[1];
    return origOpen.apply(this, arguments);
  };
  XMLHttpRequest.prototype.send = function() {
    var xhr = this;
    xhr.addEventListener('load', function() {
      try {
        var json = JSON.parse(xhr.responseText);
        var sfen = findSfenInObject(json);
        if (sfen) ShogiDroid.receiveSfen(sfen);
      } catch(e) {}
    });
    return origSend.apply(this, arguments);
  };

  function findSfenInObject(obj) {
    if (!obj || typeof obj !== 'object') return null;
    for (var key in obj) {
      if (key === 'sfen' || key === 'SFEN' || key === 'position') {
        var val = obj[key];
        if (typeof val === 'string' && /[1-9lnsgkrpbLNSGKRPB+]+\//.test(val)) {
          return val.replace(/^sfen\s+/i, '');
        }
      }
      var found = findSfenInObject(obj[key]);
      if (found) return found;
    }
    return null;
  }
})();
";
		webView.EvaluateJavascript(js, null);
	}

	// --- Inner classes ---

	private class KishinWebViewClient : WebViewClient
	{
		private readonly KishinAnalyticsActivity activity;

		public KishinWebViewClient(KishinAnalyticsActivity activity)
		{
			this.activity = activity;
		}

		public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
		{
			string url = request.Url.ToString();

			// Pattern A: Check URL for SFEN parameter
			string sfen = activity.TryExtractSfenFromUrl(url);
			if (sfen != null)
			{
				activity.OnSfenExtracted(sfen);
				return true;
			}

			return false;
		}

		public override void OnPageFinished(WebView view, string url)
		{
			base.OnPageFinished(view, url);
			activity.progressBar.Visibility = ViewStates.Gone;

			// Inject SFEN observer (Patterns B & C)
			activity.InjectSfenObserver();

			// Auto-navigate to camera scanner if logged in
			activity.TryAutoNavigateToScanner();
		}

		public override void OnPageStarted(WebView view, string url, Android.Graphics.Bitmap favicon)
		{
			base.OnPageStarted(view, url, favicon);
			activity.progressBar.Visibility = ViewStates.Visible;
		}

		public override void OnReceivedError(WebView view, IWebResourceRequest request, WebResourceError error)
		{
			base.OnReceivedError(view, request, error);
			if (request.IsForMainFrame)
			{
				view.LoadData(
					"<html><body style='text-align:center;padding:40px;'>" +
					"<h2>ネットワークエラー</h2>" +
					"<p>棋神アナリティクスに接続できません。</p>" +
					"<p>インターネット接続を確認してください。</p>" +
					"<button onclick='location.reload()'>再読み込み</button>" +
					"</body></html>",
					"text/html", "utf-8");
			}
		}

		public override void DoUpdateVisitedHistory(WebView view, string url, bool isReload)
		{
			base.DoUpdateVisitedHistory(view, url, isReload);

			// SPA navigation: check URL for SFEN on every navigation
			string sfen = activity.TryExtractSfenFromUrl(url);
			if (sfen != null)
			{
				activity.OnSfenExtracted(sfen);
			}
		}
	}

	private class KishinWebChromeClient : WebChromeClient
	{
		private readonly KishinAnalyticsActivity activity;

		public KishinWebChromeClient(KishinAnalyticsActivity activity)
		{
			this.activity = activity;
		}

		public override void OnProgressChanged(WebView view, int newProgress)
		{
			base.OnProgressChanged(view, newProgress);
			if (newProgress < 100)
			{
				activity.progressBar.Visibility = ViewStates.Visible;
			}
			else
			{
				activity.progressBar.Visibility = ViewStates.Gone;
			}
		}

		public override void OnPermissionRequest(PermissionRequest request)
		{
			activity.RunOnUiThread(() =>
			{
				// Check if Android camera permission is already granted
				if (ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.Camera) == Permission.Granted)
				{
					request.Grant(request.GetResources());
				}
				else
				{
					// Save pending request and ask for Android permission
					activity.pendingPermissionRequest = request;
					ActivityCompat.RequestPermissions(activity, new string[] { Android.Manifest.Permission.Camera }, CAMERA_PERMISSION_CODE);
				}
			});
		}

		public override bool OnShowFileChooser(WebView webView, IValueCallback filePathCallback, FileChooserParams fileChooserParams)
		{
			activity.fileUploadCallback?.OnReceiveValue(null);
			activity.fileUploadCallback = filePathCallback;

			// Check camera permission
			if (ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.Camera) != Permission.Granted)
			{
				ActivityCompat.RequestPermissions(activity, new string[] { Android.Manifest.Permission.Camera }, CAMERA_PERMISSION_CODE);
			}

			try
			{
				// Camera intent
				Intent captureIntent = new Intent(MediaStore.ActionImageCapture);
				Java.IO.File photoFile = new Java.IO.File(activity.CacheDir, $"camera_{DateTime.Now.Ticks}.jpg");
				activity.cameraImageUri = FileProvider.GetUriForFile(activity,
					"com.siganus.ShogiDroid.rebuild.provider", photoFile);
				captureIntent.PutExtra(MediaStore.ExtraOutput, activity.cameraImageUri);

				// Gallery intent
				Intent galleryIntent = new Intent(Intent.ActionGetContent);
				galleryIntent.AddCategory(Intent.CategoryOpenable);
				galleryIntent.SetType("image/*");

				// Chooser combining both
				Intent chooserIntent = Intent.CreateChooser(galleryIntent, "画像を選択");
				chooserIntent.PutExtra(Intent.ExtraInitialIntents, new Intent[] { captureIntent });

				activity.StartActivityForResult(chooserIntent, FILE_CHOOSER_CODE);
			}
			catch (Exception)
			{
				activity.fileUploadCallback?.OnReceiveValue(null);
				activity.fileUploadCallback = null;
				return false;
			}

			return true;
		}
	}

	private class ShogiDroidJsInterface : Java.Lang.Object
	{
		private readonly KishinAnalyticsActivity activity;

		public ShogiDroidJsInterface(KishinAnalyticsActivity activity)
		{
			this.activity = activity;
		}

		[Export("receiveSfen")]
		[JavascriptInterface]
		public void ReceiveSfen(string sfen)
		{
			if (!string.IsNullOrEmpty(sfen))
			{
				activity.ShowOverlay();
				activity.OnSfenExtracted(sfen);
			}
		}

		[Export("onScannerReady")]
		[JavascriptInterface]
		public void OnScannerReady()
		{
			activity.HideOverlay();
		}
	}
}
