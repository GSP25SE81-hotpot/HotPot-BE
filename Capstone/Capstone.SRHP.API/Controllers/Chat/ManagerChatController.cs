﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Chat;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Manager")]

public class ManagerChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public ManagerChatController(IChatService chatService, IHubContext<ChatHub> chatHubContext)
    {
        _chatService = chatService;
        _chatHubContext = chatHubContext;
    }

    // MANAGER-SPECIFIC ENDPOINTS

    [HttpGet("sessions/active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatSession>>>> GetActiveSessions()
    {
        var sessions = await _chatService.GetActiveChatSessionsAsync();
        return Ok(ApiResponse<IEnumerable<ChatSession>>.SuccessResponse(sessions, "Active chat sessions retrieved successfully"));
    }

    [HttpGet("sessions/manager/{managerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatSession>>>> GetManagerChatHistory(int managerId)
    {
        try
        {
            var sessions = await _chatService.GetManagerChatHistoryAsync(managerId);
            return Ok(ApiResponse<IEnumerable<ChatSession>>.SuccessResponse(sessions, "Manager chat history retrieved successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<IEnumerable<ChatSession>>.ErrorResponse(ex.Message));
        }
    }

    [HttpPut("sessions/{sessionId}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ChatSession>>> AssignManagerToSession(
    int sessionId,
    [FromBody] AssignManagerRequest request)
    {
        try
        {
            // Validate request
            if (request == null || request.ManagerId <= 0)
            {
                return BadRequest(ApiResponse<ChatSession>.ErrorResponse("Invalid request. Manager ID is required."));
            }

            var session = await _chatService.AssignManagerToChatSessionAsync(sessionId, request.ManagerId);

            // Get manager name directly from the User entity
            string managerName = "Manager";
            if (session.Manager != null)
            {
                managerName = session.Manager.Name ?? "Manager";
            }

            // Notify the customer that a manager has accepted their chat
            if (session.Customer != null)
            {
                await _chatHubContext.Clients.User(session.CustomerId.ToString()).SendAsync("ChatAccepted",
                    session.ChatSessionId,
                    request.ManagerId,
                    managerName);
            }

            // Notify other managers that this chat has been taken
            await _chatHubContext.Clients.Group("Managers").SendAsync("ChatTaken",
                sessionId,
                request.ManagerId);

            return Ok(ApiResponse<ChatSession>.SuccessResponse(session, "Manager assigned to chat session successfully"));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<ChatSession>.ErrorResponse(ex.Message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ApiResponse<ChatSession>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<ChatSession>.ErrorResponse("An error occurred while assigning the manager to the chat session. Please try again later."));
        }
    }

    // SHARED FUNCTIONALITY ENDPOINTS

    [HttpGet("messages/session/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessage>>>> GetSessionMessages(
        int sessionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var messages = await _chatService.GetSessionMessagesAsync(sessionId, pageNumber, pageSize);
            return Ok(ApiResponse<IEnumerable<ChatMessage>>.SuccessResponse(messages, "Session messages retrieved successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<IEnumerable<ChatMessage>>.ErrorResponse(ex.Message));
        }
    }

    [HttpGet("messages/unread/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessage>>>> GetUnreadMessages(int userId)
    {
        var messages = await _chatService.GetUnreadMessagesAsync(userId);
        return Ok(ApiResponse<IEnumerable<ChatMessage>>.SuccessResponse(messages, "Unread messages retrieved successfully"));
    }

    [HttpGet("messages/unread/count/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadMessageCount(int userId)
    {
        var count = await _chatService.GetUnreadMessageCountAsync(userId);
        return Ok(ApiResponse<int>.SuccessResponse(count, "Unread message count retrieved successfully"));
    }

    [HttpPut("sessions/{sessionId}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatSession>>> EndChatSession(int sessionId)
    {
        try
        {
            var session = await _chatService.EndChatSessionAsync(sessionId);

            // Notify both parties that the chat has ended
            if (session.Customer != null)
            {
                await _chatHubContext.Clients.User(session.Customer.UserId.ToString()).SendAsync("ChatEnded", sessionId);
            }

            if (session.Manager != null && session.ManagerId.HasValue)
            {
                await _chatHubContext.Clients.User(session.ManagerId.Value.ToString()).SendAsync("ChatEnded", sessionId);
            }

            return Ok(ApiResponse<ChatSession>.SuccessResponse(session, "Chat session ended successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ChatSession>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatSession>.ErrorResponse(ex.Message));
        }
    }

    [HttpPost("messages")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatMessage>>> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var message = await _chatService.SaveMessageAsync(request.SenderId, request.ReceiverId, request.Message);

            // Send the message to the receiver via SignalR
            await _chatHubContext.Clients.User(request.ReceiverId.ToString()).SendAsync("ReceiveMessage",
                message.ChatMessageId,
                message.SenderUserId,
                message.ReceiverUserId,
                message.Message,
                message.CreatedAt);

            return Ok(ApiResponse<ChatMessage>.SuccessResponse(message, "Message sent successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatMessage>.ErrorResponse(ex.Message));
        }
    }

    [HttpPut("messages/{messageId}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> MarkMessageAsRead(int messageId)
    {
        var result = await _chatService.MarkMessageAsReadAsync(messageId);
        if (!result)
            return NotFound(ApiResponse<bool>.ErrorResponse($"Message with ID {messageId} not found"));

        // Get the message to notify the sender
        var message = await _chatService.GetMessageByIdAsync(messageId);
        if (message != null)
        {
            await _chatHubContext.Clients.User(message.SenderUserId.ToString()).SendAsync("MessageRead", messageId);
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Message marked as read successfully"));
    }
}