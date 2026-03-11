using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Application.UseCases.GetSyncState;

namespace TimeTrack.AgentService.Ipc;

/// <summary>
/// Handles incoming IPC messages and routes them to appropriate use cases
/// </summary>
public sealed class IpcMessageHandler
{
    private readonly TrackingControlUseCase _trackingControl;
    private readonly GetLocalDashboardUseCase _getDashboard;
    private readonly GetSyncStateUseCase _getSyncState;
    private readonly ILogger<IpcMessageHandler> _logger;

    public IpcMessageHandler(
        TrackingControlUseCase trackingControl,
        GetLocalDashboardUseCase getDashboard,
        GetSyncStateUseCase getSyncState,
        ILogger<IpcMessageHandler> logger)
    {
        _trackingControl = trackingControl;
        _getDashboard = getDashboard;
        _getSyncState = getSyncState;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleCommandAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling command: {Name}", request.Name);

        return request.Name.ToLowerInvariant() switch
        {
            "starttracking" => await HandleStartTrackingAsync(request, cancellationToken),
            "stoptracking" => await HandleStopTrackingAsync(request, cancellationToken),
            "pausetracking" => await HandlePauseTrackingAsync(request, cancellationToken),
            "resumetracking" => await HandleResumeTrackingAsync(request, cancellationToken),
            "startfocusmode" => await HandleStartFocusModeAsync(request, cancellationToken),
            "stopfocusmode" => await HandleStopFocusModeAsync(request, cancellationToken),
            "syncnow" => await HandleSyncNowAsync(request, cancellationToken),
            "assignproject" => await HandleAssignProjectAsync(request, cancellationToken),
            "assigntask" => await HandleAssignTaskAsync(request, cancellationToken),
            "updatesettings" => await HandleUpdateSettingsAsync(request, cancellationToken),
            "setworkhours" => await HandleSetWorkHoursAsync(request, cancellationToken),
            _ => new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = $"Unknown command: {request.Name}"
            }
        };
    }

    public async Task<IpcResponse> HandleQueryAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling query: {Name}", request.Name);

        return request.Name.ToLowerInvariant() switch
        {
            "getcurrentsession" => await HandleGetCurrentSessionAsync(request, cancellationToken),
            "gettodaysummary" => await HandleGetTodaySummaryAsync(request, cancellationToken),
            "getrecentactivities" => await HandleGetRecentActivitiesAsync(request, cancellationToken),
            "getrecentapps" => await HandleGetRecentAppsAsync(request, cancellationToken),
            "getprojects" => await HandleGetProjectsAsync(request, cancellationToken),
            "gettasks" => await HandleGetTasksAsync(request, cancellationToken),
            "gettrackingstate" => await HandleGetTrackingStateAsync(request, cancellationToken),
            "getcurrentstatus" => await HandleGetCurrentStatusAsync(request, cancellationToken),
            "getsyncstate" => await HandleGetSyncStateAsync(request, cancellationToken),
            "geterrors" => await HandleGetErrorsAsync(request, cancellationToken),
            _ => new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = $"Unknown query: {request.Name}"
            }
        };
    }

    // ============================================================================
    // COMMAND HANDLERS
    // ============================================================================

    private async Task<IpcResponse> HandleStartTrackingAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _trackingControl.StartAsync(new StartTrackingRequest
            {
                StartedBy = "DesktopHost"
            }, ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IpcResponse> HandleStopTrackingAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            string? reason = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                reason = request.Payload.Value.GetProperty("reason").GetString();
            }

            var result = await _trackingControl.StopAsync(new StopTrackingRequest
            {
                StoppedBy = "DesktopHost",
                Reason = reason
            }, ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IpcResponse> HandlePauseTrackingAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            string? reason = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                reason = request.Payload.Value.GetProperty("reason").GetString();
            }

            var result = await _trackingControl.PauseAsync(new PauseTrackingRequest
            {
                Reason = reason ?? "User requested",
                PausedBy = "DesktopHost"
            }, ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IpcResponse> HandleResumeTrackingAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _trackingControl.ResumeAsync(new ResumeTrackingRequest
            {
                ResumedBy = "DesktopHost"
            }, ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private Task<IpcResponse> HandleStartFocusModeAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement focus mode
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { focusModeStarted = true })
        });
    }

    private Task<IpcResponse> HandleStopFocusModeAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement focus mode
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { focusModeStopped = true })
        });
    }

    private Task<IpcResponse> HandleSyncNowAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Trigger sync via SyncWorker
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { syncTriggered = true })
        });
    }

    private Task<IpcResponse> HandleAssignProjectAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement project assignment
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { assigned = true })
        });
    }

    private Task<IpcResponse> HandleAssignTaskAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement task assignment
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { assigned = true })
        });
    }

    private Task<IpcResponse> HandleUpdateSettingsAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement settings update
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { updated = true })
        });
    }

    private Task<IpcResponse> HandleSetWorkHoursAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement work hours
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { updated = true })
        });
    }

    // ============================================================================
    // QUERY HANDLERS
    // ============================================================================

    private Task<IpcResponse> HandleGetCurrentSessionAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get current session from repository
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new
            {
                id = Guid.NewGuid().ToString(),
                projectName = "Current Project",
                startedAt = DateTime.UtcNow.AddHours(-1),
                duration = 3600,
                isIdle = false,
                isActive = true
            })
        });
    }

    private async Task<IpcResponse> HandleGetTodaySummaryAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var dashboard = await _getDashboard.ExecuteAsync(DateTime.Today, ct);

            // Map to frontend-expected format
            var summary = new
            {
                totalDuration = (int)dashboard.TotalWorkTime.TotalMinutes,
                productiveTime = (int)dashboard.TotalWorkTime.TotalMinutes, // TODO: Calculate from categories
                idleTime = (int)dashboard.TotalIdleTime.TotalMinutes,
                focusTime = 0, // TODO: Calculate from focus sessions
                sessionsCount = dashboard.SessionCount,
                topProjects = Array.Empty<object>(), // TODO: Get from sessions
                topApplications = dashboard.TopApplications.Select(a => new
                {
                    name = a.DisplayName,
                    duration = (int)a.TotalTime.TotalMinutes,
                    percentage = a.Percentage
                }).ToArray(),
                categories = Array.Empty<object>(), // TODO: Calculate from app categories
                weeklyHistory = GenerateWeeklyHistory()
            };

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(summary)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today summary");
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private Task<IpcResponse> HandleGetRecentActivitiesAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from activity session repository
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new
            {
                activities = Array.Empty<object>(),
                total = 0
            })
        });
    }

    private Task<IpcResponse> HandleGetRecentAppsAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get recent apps from sessions
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new
            {
                apps = Array.Empty<object>(),
                since = DateTime.UtcNow.AddDays(-1)
            })
        });
    }

    private Task<IpcResponse> HandleGetProjectsAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from project repository
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(Array.Empty<object>())
        });
    }

    private Task<IpcResponse> HandleGetTasksAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from task repository
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(Array.Empty<object>())
        });
    }

    private async Task<IpcResponse> HandleGetTrackingStateAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var dashboard = await _getDashboard.ExecuteAsync(null, ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(new
                {
                    isTracking = dashboard.TrackingStatus == "Active",
                    isPaused = dashboard.TrackingStatus == "Paused",
                    isFocusMode = false
                })
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private Task<IpcResponse> HandleGetCurrentStatusAsync(IpcRequest request, CancellationToken ct)
    {
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new
            {
                state = "running",
                uptime = (int)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds,
                version = "1.0.0"
            })
        });
    }

    private async Task<IpcResponse> HandleGetSyncStateAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var state = await _getSyncState.ExecuteAsync(ct);

            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Data = JsonSerializer.SerializeToElement(state)
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private Task<IpcResponse> HandleGetErrorsAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from error repository
        return Task.FromResult(new IpcResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(new
            {
                errors = Array.Empty<object>(),
                total = 0
            })
        });
    }

    // ============================================================================
    // HELPERS
    // ============================================================================

    private static object[] GenerateWeeklyHistory()
    {
        var today = DateTime.Today;
        var history = new List<object>();

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayOfWeek = date.DayOfWeek;

            history.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                dayName = dayOfWeek switch
                {
                    DayOfWeek.Sunday => "Dom",
                    DayOfWeek.Monday => "Seg",
                    DayOfWeek.Tuesday => "Ter",
                    DayOfWeek.Wednesday => "Qua",
                    DayOfWeek.Thursday => "Qui",
                    DayOfWeek.Friday => "Sex",
                    DayOfWeek.Saturday => "Sáb",
                    _ => "???"
                },
                hours = i == 0 ? 0 : Random.Shared.Next(2, 9) + Random.Shared.NextDouble(),
                isToday = i == 0
            });
        }

        return history.ToArray();
    }
}
