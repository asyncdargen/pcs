using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace HttpMonitoring;

public partial class MainWindow : Window
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly HttpClient _httpClient = new();
    private readonly ConcurrentBag<LogEntry> _logs = new();
    private readonly ConcurrentDictionary<string, string> _messages = new();
    private int _requestCount;
    private DateTime _startTime;
    private int _getCount;
    private int _postCount;
    private long _totalProcessingTimeMs;
    private readonly ObservableCollection<ISeries> _chartSeries = new();
    private readonly List<long> _requestsPerMinute = new();
    private DispatcherTimer? _timer;

    public MainWindow()
    {
        InitializeComponent();
        LoadChart.Series = _chartSeries;
        LoadChart.XAxes = new[] { new Axis { Name = "Минута", Labels = Array.Empty<string>() } };
        LoadChart.YAxes = new[] { new Axis { Name = "Запросов" } };
    }

    private async void StartServer_Click(object? sender, RoutedEventArgs e)
    {
        var port = PortBox.Text?.Trim();
        if (string.IsNullOrEmpty(port)) return;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _cts = new CancellationTokenSource();
        _startTime = DateTime.Now;
        _requestCount = 0;
        _getCount = 0;
        _postCount = 0;
        _totalProcessingTimeMs = 0;

        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            AppendLog($"Ошибка запуска: {ex.Message}");
            return;
        }

        StartServerBtn.IsEnabled = false;
        StopServerBtn.IsEnabled = true;
        ServerStatus.Text = "Запущен";
        ServerStatus.Foreground = Avalonia.Media.Brushes.Green;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var uptime = DateTime.Now - _startTime;
            UptimeText.Text = $"Время работы: {uptime:hh\\:mm\\:ss}";
        };
        _timer.Start();

        var chartTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        chartTimer.Tick += (_, _) => UpdateChart();
        chartTimer.Start();

        AppendLog($"Сервер запущен на порту {port}");

        _ = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener!.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => AppendLog($"Ошибка: {ex.Message}"));
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var sw = Stopwatch.StartNew();
        var request = context.Request;
        var method = request.HttpMethod;
        var url = request.Url?.ToString() ?? "";
        var headers = string.Join("; ", request.Headers.AllKeys.Select(k => $"{k}: {request.Headers[k]}"));
        string body = "";
        int statusCode = 200;

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        body = reader.ReadToEnd();

        string responseText;

        try
        {
            if (method == "GET")
            {
                Interlocked.Increment(ref _getCount);
                var uptime = DateTime.Now - _startTime;
                var status = new
                {
                    requestsProcessed = _requestCount,
                    uptime = uptime.ToString(@"hh\:mm\:ss"),
                    getRequests = _getCount,
                    postRequests = _postCount
                };
                responseText = JsonSerializer.Serialize(status);
            }
            else if (method == "POST")
            {
                Interlocked.Increment(ref _postCount);
                var id = Guid.NewGuid().ToString("N")[..8];
                _messages[id] = body;
                responseText = JsonSerializer.Serialize(new { id });
            }
            else
            {
                statusCode = 405;
                responseText = JsonSerializer.Serialize(new { error = "Method not allowed" });
            }
        }
        catch (Exception ex)
        {
            statusCode = 500;
            responseText = JsonSerializer.Serialize(new { error = ex.Message });
        }

        Interlocked.Increment(ref _requestCount);
        sw.Stop();
        Interlocked.Add(ref _totalProcessingTimeMs, sw.ElapsedMilliseconds);

        var responseBytes = Encoding.UTF8.GetBytes(responseText);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = responseBytes.Length;
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        context.Response.Close();

        var entry = new LogEntry
        {
            Time = DateTime.Now,
            Method = method,
            Url = url,
            Headers = headers,
            Body = body,
            StatusCode = statusCode,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
        _logs.Add(entry);

        Dispatcher.UIThread.Post(() =>
        {
            var avg = _requestCount > 0 ? _totalProcessingTimeMs / _requestCount : 0;
            StatsText.Text = $"GET: {_getCount} | POST: {_postCount} | Среднее время: {avg} мс";
            AppendLogEntry(entry);
        });
    }

    private void StopServer_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _timer?.Stop();
        StartServerBtn.IsEnabled = true;
        StopServerBtn.IsEnabled = false;
        ServerStatus.Text = "Остановлен";
        ServerStatus.Foreground = Avalonia.Media.Brushes.Gray;
        AppendLog("Сервер остановлен");
    }

    private async void SendRequest_Click(object? sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        var method = (MethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "GET";

        try
        {
            HttpResponseMessage response;
            if (method == "POST")
            {
                var content = new StringContent(RequestBodyBox.Text ?? "", Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
                response = await _httpClient.PostAsync(url, content);
            }
            else
            {
                response = await _httpClient.GetAsync(url);
            }

            var body = await response.Content.ReadAsStringAsync();
            ResponseBox.Text = $"Status: {(int)response.StatusCode}\n\n{body}";
        }
        catch (Exception ex)
        {
            ResponseBox.Text = $"Ошибка: {ex.Message}";
        }
    }

    private void Filter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (FilterCombo == null || StatusFilterCombo == null || ServerLog == null)
            return;

        var methodFilter = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";
        var statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

        var filtered = _logs.Where(l =>
            (methodFilter == "Все" || l.Method == methodFilter) &&
            (statusFilter == "Все" || l.StatusCode.ToString() == statusFilter))
            .OrderBy(l => l.Time);

        ServerLog.Text = string.Join("\n", filtered.Select(FormatEntry));
    }

    private async void SaveLogs_Click(object? sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.txt");
        var lines = _logs.OrderBy(l => l.Time).Select(FormatEntry);
        await File.WriteAllLinesAsync(path, lines);
        AppendLog($"Логи сохранены: {path}");
    }

    private void UpdateChart()
    {
        _requestsPerMinute.Add(_requestCount);

        var values = _requestsPerMinute.ToArray();
        _chartSeries.Clear();
        _chartSeries.Add(new LineSeries<long>
        {
            Values = values.Select(v => (long)v).ToArray(),
            Name = "Запросов (накопительно)"
        });
    }

    private void AppendLog(string text)
    {
        ServerLog.Text += $"[{DateTime.Now:HH:mm:ss}] {text}\n";
    }

    private void AppendLogEntry(LogEntry entry)
    {
        var methodFilter = (FilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";
        var statusFilter = (StatusFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

        if ((methodFilter != "Все" && entry.Method != methodFilter) ||
            (statusFilter != "Все" && entry.StatusCode.ToString() != statusFilter))
            return;

        ServerLog.Text += FormatEntry(entry) + "\n";
    }

    private static string FormatEntry(LogEntry e) =>
        $"[{e.Time:HH:mm:ss}] {e.Method} {e.Url} -> {e.StatusCode} ({e.ProcessingTimeMs}ms) Headers: {e.Headers} Body: {e.Body}";
}

public class LogEntry
{
    public DateTime Time { get; set; }
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public string Headers { get; set; } = "";
    public string Body { get; set; } = "";
    public int StatusCode { get; set; }
    public long ProcessingTimeMs { get; set; }
}
