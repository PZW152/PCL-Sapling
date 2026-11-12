using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;
using Newtonsoft.Json.Linq;

namespace PCL
{
    public partial class MyMsgLogin
    {
        #region 事件

        private void OnBtn1_Click(object sender, MouseButtonEventArgs e) => Btn1_Click();
        private void OnBtn3_Click(object sender, MouseButtonEventArgs e) => Btn3_Click();

        #endregion

        private JObject Data;
        private string UserCode; // 需要用户在网页上输入的设备代码
        private string DeviceCode; // 用于轮询的设备代码
        private string Website; // 验证网页的网址

        #region 弹窗

        private readonly ModMain.MyMsgBoxConverter MyConverter;
        private readonly int Uuid = ModBase.GetUuid();

        public MyMsgLogin(ModMain.MyMsgBoxConverter Converter)
        {
            try
            {
                this.InitializeComponent();
                this.Btn1.Name = this.Btn1.Name + ModBase.GetUuid();
                this.Btn2.Name = this.Btn2.Name + ModBase.GetUuid();
                this.Btn3.Name = this.Btn3.Name + ModBase.GetUuid();
                MyConverter = Converter;
                this.ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
                Data = (JObject)Converter.Content;
                Init();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "登录弹窗初始化失败", LogBehavior.Toast);
            }

            base.Loaded += Load;
        }

        private void Load(object sender, EventArgs e)
        {
            try
            {
                // 动画
                this.Opacity = 0d;
                ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, (MyConverter.IsWarn ? new ModBase.MyColor(140d, 80d, 0d, 0d) : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsg.Background, 200), "PanMsg Background");
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this, 1d, 120, 60), ModAnimation.AaDouble(i => this.TransformPos.Y = this.TransformPos.Y + Coerce.ToDouble(i), -this.TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaDouble(i => this.TransformRotate.Angle = this.TransformRotate.Angle + Coerce.ToDouble(i), -this.TransformRotate.Angle, 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "MyMsgBox " + Uuid);
                // 记录日志
                Logger.Info($"登录弹窗：{this.LabTitle.Text}{ModBase.CrLf}{this.LabCaption.Text}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "登录弹窗加载失败", LogBehavior.Toast);
            }
        }
        private void Close()
        {
            // 动画
            ModAnimation.AniStart(new[] { ModAnimation.AaCode(() => { if (!ModMain.WaitingMyMsgBox.Any()) { ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsg.Background, 200, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))); } }, 30), ModAnimation.AaOpacity(this, -this.Opacity, 80, 20), ModAnimation.AaDouble(i => this.TransformPos.Y = this.TransformPos.Y + Coerce.ToDouble(i), 20d - this.TransformPos.Y, 150, 0, new ModAnimation.AniEaseOutFluent()), ModAnimation.AaDouble(i => this.TransformRotate.Angle = this.TransformRotate.Angle + Coerce.ToDouble(i), 6d - this.TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => ((Grid)this.Parent).Children.Remove(this), After: true) }, "MyMsgBox " + Uuid);
        }

        // 实现回车和 Esc 的接口（#4857）
        public void Btn1_Click()
        {
        }
        public void Btn3_Click()
        {
            Finished(new OperationCanceledException());
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            // 原 VB 為 On Error Resume Next：出錯即跳過該語句
            try { if (e.GetPosition(this.ShapeLine).Y <= 2d) ModMain.FrmMain.DragMove(); } catch { }
        }

        #endregion

        private void Finished(object Result)
        {
            if (MyConverter.IsExited)
                return;
            MyConverter.IsExited = true;
            MyConverter.Result = Result;
            ModBase.RunInUi(Close);
            Thread.Sleep(200);
            ModMain.FrmMain.ShowWindowToTop();
        }

        private void Init()
        {
            UserCode = (string)Data["user_code"];
            DeviceCode = (string)Data["device_code"];
            Website = (string)Data["verification_uri"];
            // 设置 UI
            this.LabTitle.Text = "登录 Minecraft";
            this.LabCaption.Text = $"登录网页将自动开启，请在网页中输入 {UserCode}（已自动复制）。" + ModBase.CrLf + ModBase.CrLf + $"如果网络环境不佳，网页可能一直加载不出来，届时请使用加速器或 VPN 改善网络环境。" + ModBase.CrLf + $"你也可以用其他设备打开 {Website} 并输入上述代码。";
            CustomEventService.SetEventData(this.Btn1, Website);
            CustomEventService.SetEventData(this.Btn2, UserCode);
            // 登录页是微软的固定网址时，显示二维码，方便用手机完成登录
            if (MicrosoftSignIn.CanShowQr(Website))
            {
                this.PanQr.Visibility = System.Windows.Visibility.Visible;
                this.LabCaption.Text += ModBase.CrLf + ModBase.CrLf + "也可以用手机扫描右侧的二维码，在手机上输入上面的代码完成登录。";
            }
            // 启动工作线程
            ModBase.RunInDedicatedThread(WorkThread, "MyMsgLogin");
        }

        private void WorkThread()
        {
            Thread.Sleep(2000);
            if (MyConverter.IsExited)
                return;
            ModBase.OpenWebsite(Website);
            ModBase.ClipboardSet(UserCode);
            Thread.Sleep((Data["interval"].ToObject<int>() - 1) * 1000);
            // 轮询
            int UnknownFailureCount = 0;
            while (!MyConverter.IsExited)
            {
                try
                {
                    string Result = ModNet.NetRequestByClient("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", HttpMethod.Post, Content: "grant_type=urn:ietf:params:oauth:grant-type:device_code" + "&" + "client_id=" + ModSecret.OAuthClientId + "&" + "device_code=" + DeviceCode + "&" + "scope=XboxLive.signin%20offline_access", ContentType: "application/x-www-form-urlencoded", Timeout: 5000 + UnknownFailureCount * 5000, MakeLog: false, RequireJson: true);
                    // 获取结果
                    JObject ResultJson = (JObject)Result.DeserializeJson();
                    ModLaunch.McLaunchLog($"令牌过期时间：{ResultJson["expires_in"]} 秒");
                    ModMain.Hint("网页登录成功！", ModMain.HintType.Green);
                    Finished(new[] { ResultJson["access_token"].ToString(), ResultJson["refresh_token"].ToString() });
                    return;
                }
                catch (Exception ex)
                {
                    // 修改错误列表时，同时修改 ModLaunch.MsLoginStep1Refresh 中的对应代码
                    if (ex is HttpRequestCodeException)
                    {
                        string Response = ((HttpRequestCodeException)ex).Response;
                        if (Response.Contains("authorization_declined"))
                        {
                            Finished(new Exception("$你拒绝了 PCL 申请的权限……"));
                            return;
                        }
                        else if (Response.Contains("expired_token"))
                        {
                            Finished(new Exception("$登录用时太长啦，重新试试吧！"));
                            return;
                        }
                        else if (Response.Contains("Account security interrupt"))
                        {
                            Finished(new Exception("$该账号由于安全问题无法登陆，请前往微软账户页获取更多信息。"));
                            return;
                        }
                        else if (Response.Contains("service abuse"))
                        {
                            Finished(new Exception("$非常抱歉，该账号已被微软封禁，无法登录。"));
                            return;
                        }
                        else if (Response.Contains("AADSTS70000")) // 可能不能判 “invalid_grant”，见 #269
                        {
                            Finished(new ModBase.RestartException());
                            return;
                        }
                        else if (Response.Contains("authorization_pending"))
                        {
                            Thread.Sleep(2000);
                            continue;
                        }
                    }
                    if (UnknownFailureCount <= 2)
                    {
                        UnknownFailureCount += 1;
                        Logger.Warn(ex, $"登录轮询第 {UnknownFailureCount} 次失败");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        Finished(new Exception("登录轮询失败", ex));
                        return;
                    }
                }
            }
        }

    }
}