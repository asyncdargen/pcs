using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NetworkAnalyzer;

public partial class MainWindow : Window
{
    private readonly List<NetworkInterface> _interfaces = new();
    private readonly List<string> _urlHistory = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadNetworkInterfaces();
    }

    // ============ Сетевые интерфейсы ============

    private void LoadNetworkInterfaces()
    {
        _interfaces.Clear();
        InterfacesList.Items.Clear();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            _interfaces.Add(ni);
            var status = ni.OperationalStatus == OperationalStatus.Up ? "●" : "○";
            InterfacesList.Items.Add($"{status} {ni.Name} ({ni.NetworkInterfaceType})");
        }
    }

    private void OnRefreshInterfaces(object? sender, RoutedEventArgs e)
    {
        LoadNetworkInterfaces();
        InterfaceDetails.Text = "Выберите интерфейс из списка слева";
    }

    private void OnInterfaceSelected(object? sender, SelectionChangedEventArgs e)
    {
        var index = InterfacesList.SelectedIndex;
        if (index < 0 || index >= _interfaces.Count) return;

        var ni = _interfaces[index];
        var props = ni.GetIPProperties();
        var sb = new StringBuilder();

        sb.AppendLine($"Имя: {ni.Name}");
        sb.AppendLine($"Описание: {ni.Description}");
        sb.AppendLine($"Тип: {ni.NetworkInterfaceType}");
        sb.AppendLine($"Состояние: {TranslateStatus(ni.OperationalStatus)}");
        sb.AppendLine($"MAC-адрес: {FormatMac(ni.GetPhysicalAddress())}");
        sb.AppendLine($"Скорость: {FormatSpeed(ni.Speed)}");
        sb.AppendLine();

        var unicast = props.UnicastAddresses;
        if (unicast.Count > 0)
        {
            sb.AppendLine("IP-адреса:");
            foreach (var addr in unicast)
            {
                sb.Append($"  {addr.Address}");
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.IPv4Mask != null)
                    sb.Append($" (маска: {addr.IPv4Mask})");
                sb.AppendLine();
            }
        }

        var gateways = props.GatewayAddresses;
        if (gateways.Count > 0)
        {
            sb.AppendLine("Шлюзы:");
            foreach (var gw in gateways)
                sb.AppendLine($"  {gw.Address}");
        }

        var dnsServers = props.DnsAddresses;
        if (dnsServers.Count > 0)
        {
            sb.AppendLine("DNS-серверы:");
            foreach (var dns in dnsServers)
                sb.AppendLine($"  {dns}");
        }

        InterfaceDetails.Text = sb.ToString();
    }

    private static string TranslateStatus(OperationalStatus status) => status switch
    {
        OperationalStatus.Up => "Подключён",
        OperationalStatus.Down => "Отключён",
        OperationalStatus.LowerLayerDown => "Нижний уровень отключён",
        OperationalStatus.Testing => "Тестирование",
        OperationalStatus.Dormant => "В спящем режиме",
        OperationalStatus.NotPresent => "Отсутствует",
        _ => status.ToString()
    };

    private static string FormatMac(PhysicalAddress mac)
    {
        var bytes = mac.GetAddressBytes();
        return bytes.Length == 0 ? "Н/Д" : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string FormatSpeed(long speed)
    {
        if (speed <= 0) return "Н/Д";
        if (speed >= 1_000_000_000) return $"{speed / 1_000_000_000.0:F1} Гбит/с";
        if (speed >= 1_000_000) return $"{speed / 1_000_000.0:F1} Мбит/с";
        if (speed >= 1_000) return $"{speed / 1_000.0:F1} Кбит/с";
        return $"{speed} бит/с";
    }

    // ============ Анализ URL ============

    private void OnUrlInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            AnalyzeUrl();
    }

    private void OnAnalyzeUrl(object? sender, RoutedEventArgs e) => AnalyzeUrl();

    private void AnalyzeUrl()
    {
        var input = UrlInput.Text?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        // Добавить схему, если отсутствует
        if (!input.Contains("://"))
            input = "https://" + input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            ResultsOutput.Text = "Ошибка: введён некорректный URL.";
            ClearUrlFields();
            return;
        }

        UrlScheme.Text = uri.Scheme;
        UrlHost.Text = uri.Host;
        UrlPort.Text = uri.IsDefaultPort ? $"{uri.Port} (по умолчанию)" : uri.Port.ToString();
        UrlPath.Text = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/" ? "/" : uri.AbsolutePath;
        UrlQuery.Text = string.IsNullOrEmpty(uri.Query) ? "—" : uri.Query;
        UrlFragment.Text = string.IsNullOrEmpty(uri.Fragment) ? "—" : uri.Fragment;

        // Определить тип адреса
        UrlAddressType.Text = DetectAddressType(uri.Host);

        // DNS-информация
        ResolveDns(uri.Host);

        // Добавить в историю
        AddToHistory(input);

        ResultsOutput.Text = $"URL \"{input}\" успешно проанализирован.";
    }

    private void ClearUrlFields()
    {
        UrlScheme.Text = "";
        UrlHost.Text = "";
        UrlPort.Text = "";
        UrlPath.Text = "";
        UrlQuery.Text = "";
        UrlFragment.Text = "";
        UrlAddressType.Text = "";
        UrlDns.Text = "";
    }

    private static string DetectAddressType(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
            return "Доменное имя (не IP)";

        if (IPAddress.IsLoopback(ip))
            return "Loopback (петлевой)";

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal)
            return "Link-local (IPv6)";

        // Проверка приватных диапазонов IPv4
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 10)
                return "Локальный (приватный, 10.0.0.0/8)";
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return "Локальный (приватный, 172.16.0.0/12)";
            if (bytes[0] == 192 && bytes[1] == 168)
                return "Локальный (приватный, 192.168.0.0/16)";
            if (bytes[0] == 169 && bytes[1] == 254)
                return "Link-local (169.254.0.0/16)";
        }

        return "Публичный";
    }

    private async void ResolveDns(string host)
    {
        UrlDns.Text = "Определение...";
        try
        {
            var entry = await Dns.GetHostEntryAsync(host);
            var sb = new StringBuilder();
            sb.Append($"Имя хоста: {entry.HostName}");
            var addresses = entry.AddressList;
            if (addresses.Length > 0)
            {
                sb.Append(" | IP: ");
                sb.Append(string.Join(", ", addresses.Select(a => a.ToString())));
            }
            UrlDns.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            UrlDns.Text = $"Ошибка DNS: {ex.Message}";
        }
    }

    // ============ Ping ============

    private async void OnPingHost(object? sender, RoutedEventArgs e)
    {
        var input = UrlInput.Text?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (!input.Contains("://"))
            input = "https://" + input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            ResultsOutput.Text = "Ошибка: введён некорректный URL для ping.";
            return;
        }

        var host = uri.Host;
        PingBtn.IsEnabled = false;
        ResultsOutput.Text = $"Выполняется ping {host}...";

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Ping {host}:");
            sb.AppendLine();

            using var ping = new Ping();
            long totalMs = 0;
            int success = 0;
            const int count = 4;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(host, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        sb.AppendLine($"  Ответ от {reply.Address}: время={reply.RoundtripTime}мс TTL={reply.Options?.Ttl}");
                        totalMs += reply.RoundtripTime;
                        success++;
                    }
                    else
                    {
                        sb.AppendLine($"  {TranslatePingStatus(reply.Status)}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  Ошибка: {ex.Message}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Статистика: отправлено={count}, получено={success}, потеряно={count - success}");
            if (success > 0)
                sb.AppendLine($"Среднее время: {totalMs / success}мс");

            sb.AppendLine();
            sb.Append($"Хост {host} ");
            sb.AppendLine(success > 0 ? "ДОСТУПЕН" : "НЕДОСТУПЕН");

            ResultsOutput.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            ResultsOutput.Text = $"Ошибка ping: {ex.Message}";
        }
        finally
        {
            PingBtn.IsEnabled = true;
        }
    }

    private static string TranslatePingStatus(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "Превышен интервал ожидания",
        IPStatus.DestinationHostUnreachable => "Узел назначения недоступен",
        IPStatus.DestinationNetworkUnreachable => "Сеть назначения недоступна",
        _ => status.ToString()
    };

    // ============ История URL ============

    private void AddToHistory(string url)
    {
        if (_urlHistory.Contains(url)) return;
        _urlHistory.Insert(0, url);
        UrlHistoryList.Items.Insert(0, url);
    }

    private void OnHistoryItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (UrlHistoryList.SelectedItem is string url)
        {
            UrlInput.Text = url;
            AnalyzeUrl();
        }
    }

    private void OnClearHistory(object? sender, RoutedEventArgs e)
    {
        _urlHistory.Clear();
        UrlHistoryList.Items.Clear();
    }
}
