using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Services.Acme;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Controllers.Acme
{
    /// <summary>
    /// 证书申请进度跟踪控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CertificateProgressController : ControllerBase
    {
        private readonly ICertificateProgressService _progressService;
        private readonly ILogger<CertificateProgressController> _logger;

        public CertificateProgressController(
            ICertificateProgressService progressService,
            ILogger<CertificateProgressController> logger)
        {
            _progressService = progressService;
            _logger = logger;
        }

        /// <summary>
        /// 创建证书申请进度跟踪
        /// </summary>
        /// <param name="request">进度跟踪请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进度跟踪ID</returns>
        [HttpPost("create")]
        public async Task<ActionResult<string>> CreateProgressAsync(
            [FromBody] ProgressTrackRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var progressId = await _progressService.CreateProgressAsync(request);
                return Ok(new { ProgressId = progressId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建进度跟踪失败");
                return StatusCode(500, new { Message = "创建进度跟踪失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书申请进度
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进度跟踪信息</returns>
        [HttpGet("{progressId}")]
        public async Task<ActionResult<ProgressTrackResponse>> GetProgressAsync(
            string progressId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var progress = await _progressService.GetProgressAsync(progressId);
                // 即使没有进度记录也返回200 OK，而不是404
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取进度信息失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "获取进度信息失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 根据证书ID获取申请进度
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进度跟踪信息</returns>
        [HttpGet("by-certificate/{certificateId}")]
        public async Task<ActionResult<ProgressTrackResponse>> GetProgressByCertificateIdAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var progress = await _progressService.GetProgressByCertificateIdAsync(certificateId);
                // 即使没有进度记录也返回200 OK，而不是404
                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书进度信息失败: {CertificateId}", certificateId);
                return StatusCode(500, new { Message = "获取证书进度信息失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取所有进度跟踪列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>进度跟踪列表</returns>
        [HttpGet]
        public async Task<ActionResult<List<ProgressTrackResponse>>> GetAllProgressAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var progressList = await _progressService.GetAllProgressAsync();
                return Ok(progressList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有进度列表失败");
                return StatusCode(500, new { Message = "获取所有进度列表失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 更新进度步骤
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="request">更新请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPut("{progressId}/step")]
        public async Task<ActionResult> UpdateProgressStepAsync(
            string progressId,
            [FromBody] UpdateProgressStepRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.UpdateProgressStepAsync(progressId, request.Step, request.Message, request.IsCompleted);
                return Ok(new { Message = "进度步骤更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新进度步骤失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "更新进度步骤失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 完成当前步骤
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="request">完成请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPut("{progressId}/complete-current")]
        public async Task<ActionResult> CompleteCurrentStepAsync(
            string progressId,
            [FromBody] CompleteCurrentStepRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.CompleteCurrentStepAsync(progressId, request.Message);
                return Ok(new { Message = "当前步骤完成成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成当前步骤失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "完成当前步骤失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 添加错误信息
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="request">错误请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPost("{progressId}/error")]
        public async Task<ActionResult> AddErrorAsync(
            string progressId,
            [FromBody] AddErrorRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.AddErrorAsync(progressId, request.Error);
                return Ok(new { Message = "错误信息添加成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加错误信息失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "添加错误信息失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="request">警告请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPost("{progressId}/warning")]
        public async Task<ActionResult> AddWarningAsync(
            string progressId,
            [FromBody] AddWarningRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.AddWarningAsync(progressId, request.Warning);
                return Ok(new { Message = "警告信息添加成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加警告信息失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "添加警告信息失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 标记进度完成
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPut("{progressId}/complete")]
        public async Task<ActionResult> MarkAsCompletedAsync(
            string progressId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.MarkAsCompletedAsync(progressId);
                return Ok(new { Message = "进度标记完成成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记进度完成失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "标记进度完成失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 标记进度失败
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="request">失败请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPut("{progressId}/fail")]
        public async Task<ActionResult> MarkAsFailedAsync(
            string progressId,
            [FromBody] MarkAsFailedRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.MarkAsFailedAsync(progressId, request.ErrorMessage);
                return Ok(new { Message = "进度标记失败成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记进度失败失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "标记进度失败失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 删除进度记录
        /// </summary>
        /// <param name="progressId">进度跟踪ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpDelete("{progressId}")]
        public async Task<ActionResult> DeleteProgressAsync(
            string progressId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.DeleteProgressAsync(progressId);
                return Ok(new { Message = "进度记录删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除进度记录失败: {ProgressId}", progressId);
                return StatusCode(500, new { Message = "删除进度记录失败", Error = ex.Message });
            }
        }

        /// <summary>
        /// 清理过期的进度记录
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPost("cleanup")]
        public async Task<ActionResult> CleanupExpiredProgressAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _progressService.CleanupExpiredProgressAsync();
                return Ok(new { Message = "过期进度记录清理成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期进度记录失败");
                return StatusCode(500, new { Message = "清理过期进度记录失败", Error = ex.Message });
            }
        }
    }

    #region 请求模型

    /// <summary>
    /// 更新进度步骤请求
    /// </summary>
    public class UpdateProgressStepRequest
    {
        /// <summary>
        /// 步骤
        /// </summary>
        public CertificateApplicationStep Step { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted { get; set; } = false;
    }

    /// <summary>
    /// 完成当前步骤请求
    /// </summary>
    public class CompleteCurrentStepRequest
    {
        /// <summary>
        /// 消息
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// 添加错误请求
    /// </summary>
    public class AddErrorRequest
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// 添加警告请求
    /// </summary>
    public class AddWarningRequest
    {
        /// <summary>
        /// 警告信息
        /// </summary>
        public string Warning { get; set; } = string.Empty;
    }

    /// <summary>
    /// 标记失败请求
    /// </summary>
    public class MarkAsFailedRequest
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }

    #endregion
}