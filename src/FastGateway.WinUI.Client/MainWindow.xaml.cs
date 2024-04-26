using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using FastGateway.TunnelClient;

namespace FastGateway.WinUI.Client;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool status;
    private WebApplication webApplication;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnConnect()
    {
        try
        {

            // 验证txtServer是否合法
            if (string.IsNullOrWhiteSpace(txtServer.Text))
            {
                MessageBox.Show("服务器地址不能为空");
                return;
            }

            // 是否是https/http/ws/wss
            if (!txtServer.Text.StartsWith("http://") && !txtServer.Text.StartsWith("https://") &&
                !txtServer.Text.StartsWith("ws://") && !txtServer.Text.StartsWith("wss://"))
            {
                MessageBox.Show("服务器地址格式错误");
                return;
            }

            statusIndicator.Fill = status ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseTunnelTransport(txtServer.Text);

            builder.Services.AddReverseProxy();

            // 连接成功后，保存配置

            if (status)
            {
                JsonSave_OnClick(null, null);
            }

            webApplication = builder.Build();

            webApplication.RunAsync();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }

    private void JsonSave_OnClick(object sender, RoutedEventArgs e)
    {
        // 获取json内容
        var json = jsonInput.Text;

        // 验证json合法性
        try
        {
            var obj = JsonSerializer.Deserialize<object>(json);
        }
        catch (Exception exception)
        {
            MessageBox.Show("Json格式错误:" + exception.Message);
            return;
        }

        // 保存到文件
        File.WriteAllText("appsettings.json", json);
    }

    private void Connect_OnClick(object sender, RoutedEventArgs e)
    {
        // 连接服务器
        status = !status;
        OnConnect();


        // 连接成功后，保存配置
        if (status)
        {
            JsonSave_OnClick(sender, e);
        }
    }
}