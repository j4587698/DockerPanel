using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 后台任务 Minimal API 端点（原 TasksController）。
    /// </summary>
    public static class TaskEndpoints
    {
        /// <summary>
        /// 映射任务相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/tasks");

            group.MapGet("/", GetTasks);
            group.MapGet("/active", GetActiveTasks);
            group.MapGet("/{id}", GetTask);
            group.MapDelete("/{id}", RemoveTask);
            group.MapPost("/clear-completed", ClearCompleted);

            return app;
        }

        private static Results<Ok<List<BackgroundTask>>, NotFound<ApiErrorResponse>> GetTasks(BackgroundTaskService taskService)
        {
            return TypedResults.Ok(taskService.GetAllTasks());
        }

        private static Ok<List<BackgroundTask>> GetActiveTasks(BackgroundTaskService taskService)
        {
            return TypedResults.Ok(taskService.GetActiveTasks());
        }

        private static Results<Ok<BackgroundTask>, NotFound<ApiErrorResponse>> GetTask(string id, BackgroundTaskService taskService)
        {
            var task = taskService.GetTask(id);
            return task == null
                ? TypedResults.NotFound(new ApiErrorResponse { Error = "任务不存在" })
                : TypedResults.Ok(task);
        }

        private static Ok<TaskActionResponse> RemoveTask(string id, BackgroundTaskService taskService, ILocalizationService localization)
        {
            taskService.RemoveTask(id);
            return TypedResults.Ok(new TaskActionResponse { Id = id, Message = localization.GetMessage("task.deleted") });
        }

        private static Ok<TaskActionResponse> ClearCompleted(BackgroundTaskService taskService, ILocalizationService localization)
        {
            taskService.ClearCompletedTasks();
            return TypedResults.Ok(new TaskActionResponse { Message = localization.GetMessage("task.cleared") });
        }
    }
}