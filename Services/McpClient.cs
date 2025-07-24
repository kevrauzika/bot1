using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace Microsoft.BotBuilderSamples.Services
{
    public class McpClient : IDisposable
    {
        private readonly ILogger<McpClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private Process _mcpProcess;  
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private readonly object _lockObject = new object();

        public McpClient(ILogger<McpClient> logger, IConfiguration configuration, IHostEnvironment environment)
        {
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        public async Task<string?> ExecuteToolAsync(string toolName, object parameters)
        {
            try
            {
                await EnsureInitializedAsync();

                if (_mcpProcess == null || _mcpProcess.HasExited)
                {
                    _logger.LogWarning("MCP Server não está disponível");
                    return null;
                }

                lock (_lockObject)
                {
                    if (_mcpProcess.HasExited)
                    {
                        _logger.LogWarning("MCP Server foi encerrado inesperadamente");
                        return null;
                    }

                    try
                    {
                        var request = new
                        {
                            jsonrpc = "2.0",
                            id = Guid.NewGuid().ToString(),
                            method = "tools/call",
                            @params = new
                            {
                                name = toolName,
                                arguments = parameters
                            }
                        };

                        var requestJson = JsonSerializer.Serialize(request);
                        _logger.LogDebug("Enviando comando MCP: {Command}", requestJson);

                        _mcpProcess.StandardInput.WriteLine(requestJson);
                        _mcpProcess.StandardInput.Flush();

                        // Timeout de 10 segundos para resposta
                        var responseTask = _mcpProcess.StandardOutput.ReadLineAsync();
                        if (responseTask.Wait(10000))
                        {
                            var response = responseTask.Result;
                            _logger.LogDebug("Resposta MCP recebida: {Response}", response);
                            return response;
                        }
                        else
                        {
                            _logger.LogWarning("Timeout ao aguardar resposta do MCP Server para: {ToolName}", toolName);
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro na comunicação com MCP Server para ferramenta: {ToolName}", toolName);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar ferramenta MCP: {ToolName}", toolName);
                return null;
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            await _initSemaphore.WaitAsync();
            try
            {
                if (_isInitialized) return;

                // Verificar se MCP está habilitado na configuração
                var mcpEnabled = _configuration.GetValue<bool>("McpServer:Enabled", true);
                if (!mcpEnabled)
                {
                    _logger.LogInformation("MCP Server está desabilitado na configuração");
                    _isInitialized = true;
                    return;
                }

                await StartMcpServerAsync();
                _isInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        private async Task StartMcpServerAsync()
        {
            try
            {
                var serverPath = Path.Combine(_environment.ContentRootPath, "mcp-server", "server.js");

                if (!File.Exists(serverPath))
                {
                    _logger.LogWarning("MCP Server não encontrado em: {ServerPath}. Continuando sem MCP.", serverPath);
                    return;
                }

                // Verificar se Node.js está disponível
                if (!IsNodeJsAvailable())
                {
                    _logger.LogWarning("Node.js não encontrado. MCP Server não será iniciado.");
                    return;
                }

                _logger.LogInformation("Iniciando MCP Server: {ServerPath}", serverPath);

                _mcpProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = serverPath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.Combine(_environment.ContentRootPath, "mcp-server")
                    }
                };

                // Capturar erros do processo
                _mcpProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogError("MCP Server Error: {Error}", e.Data);
                    }
                };

                _mcpProcess.Start();
                _mcpProcess.BeginErrorReadLine();

                _logger.LogInformation("MCP Server iniciado com PID: {ProcessId}", _mcpProcess.Id);

                // Aguardar inicialização
                await Task.Delay(3000);

                if (_mcpProcess.HasExited)
                {
                    _logger.LogError("MCP Server falhou ao iniciar. Exit code: {ExitCode}", _mcpProcess.ExitCode);
                    _mcpProcess = null;
                    return;
                }

                _logger.LogInformation("MCP Server inicializado com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar MCP Server");
                _mcpProcess = null;
            }
        }

        private bool IsNodeJsAvailable()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                await EnsureInitializedAsync();
                return _mcpProcess != null && !_mcpProcess.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> TestConnectionAsync()
        {
            try
            {
                return await ExecuteToolAsync("get_system_info", new { });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão MCP");
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_mcpProcess != null && !_mcpProcess.HasExited)
                {
                    _logger.LogInformation("Encerrando MCP Server...");

                    // Tentar encerrar graciosamente
                    _mcpProcess.StandardInput.Close();

                    if (!_mcpProcess.WaitForExit(5000))
                    {
                        _logger.LogWarning("MCP Server não encerrou graciosamente, forçando encerramento...");
                        _mcpProcess.Kill();
                    }

                    _mcpProcess.Dispose();
                    _logger.LogInformation("MCP Server encerrado");
                }

                _initSemaphore.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao finalizar MCP Server");
            }
        }
    }
}