using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using MeloongCore;
using MeloongCore.Extensions;
using MeloongCore.Wpf.Extensions;

namespace PCL
{
    public partial class MyMsgInput
    {
        #region 事件

        private void OnBtn1_Click(object sender, MouseButtonEventArgs e) => Btn1_Click();
        private void OnBtn2_Click(object sender, MouseButtonEventArgs e) => Btn2_Click();

        #endregion


        private readonly ModMain.MyMsgBoxConverter MyConverter;
        private readonly int Uuid = ModBase.GetUuid();

        public MyMsgInput(ModMain.MyMsgBoxConverter Converter)
        {
            try
            {

                this.InitializeComponent();
                MyTextBox.ValidateChanged += TextCaption_ValidateChanged; // VB 中以 Handles 訂閱的是靜態事件
                this.Btn1.Name = this.Btn1.Name + ModBase.GetUuid();
                this.Btn2.Name = this.Btn2.Name + ModBase.GetUuid();
                MyConverter = Converter;
                this.LabTitle.Text = Converter.Title;
                this.LabText.Text = Converter.Text;
                this.PanText.Visibility = string.IsNullOrEmpty(Converter.Text) ? Visibility.Collapsed : Visibility.Visible;
                this.TextArea.Text = Coerce.ToStr(Converter.Content);
                this.TextArea.HintText = Converter.HintText;
                this.TextArea.ValidateRules = Converter.ValidateRules;
                this.Btn1.Text = Converter.Button1;
                if (Converter.IsWarn)
                {
                    this.Btn1.ColorType = MyButton.ColorState.Red;
                    this.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
                }
                this.Btn2.Text = Converter.Button2;
                this.Btn2.Visibility = string.IsNullOrEmpty(Converter.Button2) ? Visibility.Collapsed : Visibility.Visible;
                this.ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            }

            catch (Exception ex)
            {
                Logger.Error(ex, "输入弹窗初始化失败", LogBehavior.Toast);
            }

            base.Loaded += Load;
        }

        private void Load(object sender, EventArgs e)
        {
            try
            {

                // UI 初始化
                if (this.Btn2.IsVisible && !(this.Btn1.ColorType == MyButton.ColorState.Red))
                    this.Btn1.ColorType = MyButton.ColorState.Highlight;
                this.TextArea.Focus();
                this.TextArea.SelectionStart = this.TextArea.Text.Length;
                // 动画
                this.Opacity = 0d;
                ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, (MyConverter.IsWarn ? new ModBase.MyColor(140d, 80d, 0d, 0d) : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsg.Background, 200), "PanMsg Background");
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(this, 1d, 120, 60), ModAnimation.AaDouble(i => this.TransformPos.Y = this.TransformPos.Y + Coerce.ToDouble(i), -this.TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)), ModAnimation.AaDouble(i => this.TransformRotate.Angle = this.TransformRotate.Angle + Coerce.ToDouble(i), -this.TransformRotate.Angle, 300, 60, new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)) }, "MyMsgBox " + Uuid);
                // 记录日志
                Logger.Info($"输入弹窗：{this.LabTitle.Text}");
            }

            catch (Exception ex)
            {
                Logger.Error(ex, "输入弹窗加载失败", LogBehavior.Toast);
            }
        }
        private void Close()
        {
            // 结束线程阻塞
            MyConverter.WaitFrame.Continue = false;
            System.Windows.Interop.ComponentDispatcher.PopModal();
            // 动画
            ModAnimation.AniStart(new[] { ModAnimation.AaCode(() => { if (!ModMain.WaitingMyMsgBox.Any()) { ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsg, Panel.BackgroundProperty, new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsg.Background, 200, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))); } }, 30), ModAnimation.AaOpacity(this, -this.Opacity, 80, 20), ModAnimation.AaDouble(i => this.TransformPos.Y = this.TransformPos.Y + Coerce.ToDouble(i), 20d - this.TransformPos.Y, 150, 0, new ModAnimation.AniEaseOutFluent()), ModAnimation.AaDouble(i => this.TransformRotate.Angle = this.TransformRotate.Angle + Coerce.ToDouble(i), 6d - this.TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)), ModAnimation.AaCode(() => ((Grid)this.Parent).Children.Remove(this), After: true) }, "MyMsgBox " + Uuid);
        }

        public void Btn1_Click()
        {
            this.TextArea.Validate(); // #5773
            if (MyConverter.IsExited || !this.TextArea.IsValidated)
                return;
            MyConverter.IsExited = true;
            MyConverter.Result = this.TextArea.Text;
            Close();
        }
        public void Btn2_Click()
        {
            if (MyConverter.IsExited)
                return;
            MyConverter.IsExited = true;
            MyConverter.Result = null;
            Close();
        }

        private void TextCaption_ValidateChanged(object sender, EventArgs e)
        {
            this.Btn1.IsEnabled = this.TextArea.IsValidated;
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            // 原 VB 為 On Error Resume Next：出錯即跳過該語句
            try { if (e.GetPosition(this.ShapeLine).Y <= 2d) ModMain.FrmMain.DragMove(); } catch { }
        }

    }
}