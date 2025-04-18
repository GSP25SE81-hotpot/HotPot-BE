﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Chat;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/customer/chat")]
[ApiController]
[Authorize(Roles = "Customer")]
public class CustomerChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public CustomerChatController(IChatService chatService, IHubContext<ChatHub> chatHubContext)
    {
        _chatService = chatService;
        _chatHubContext = chatHubContext;
    }

    // CUSTOMER-SPECIFIC ENDPOINTS

    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatSessionDetailDto>>> GetChatSession(int sessionId)
    {
        try
        {
            var session = await _chatService.GetChatSessionAsync(sessionId);
            if (session == null)
                return NotFound(ApiResponse<ChatSessionDetailDto>.ErrorResponse($"Chat session with ID {sessionId} not found"));

            return Ok(ApiResponse<ChatSessionDetailDto>.SuccessResponse(session, "Chat session retrieved successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatSessionDetailDto>.ErrorResponse(ex.Message));
        }
    }

    [HttpPost("sessions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ChatSessionDto>>> CreateChatSession([FromBody] CreateChatSessionRequest request)
    {
        try
        {
            // Validate request
            if (request == null || request.CustomerId <= 0 || string.IsNullOrWhiteSpace(request.Topic))
            {
                return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse("Invalid request. Customer ID and topic are required."));
            }

            var session = await _chatService.CreateChatSessionAsync(request.CustomerId, request.Topic);

            // Notify managers about the new chat request via SignalR
            await _chatHubContext.Clients.Group("Managers").SendAsync("NewChatRequest",
                session.ChatSessionId,
                session.CustomerId,
                session.CustomerName,
                session.Topic,
                session.CreatedAt);

            return CreatedAtAction(nameof(GetChatSession), new { sessionId = session.ChatSessionId },
                ApiResponse<ChatSessionDto>.SuccessResponse(session, "Chat session created successfully"));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<ChatSessionDto>.ErrorResponse("An error occurred while creating the chat session. Please try again later."));
        }
    }

    // SHARED FUNCTIONALITY ENDPOINTS

    [HttpGet("messages/session/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessageDto>>>> GetSessionMessages(
        int sessionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var messages = await _chatService.GetSessionMessagesAsync(sessionId, pageNumber, pageSize);
            return Ok(ApiResponse<IEnumerable<ChatMessageDto>>.SuccessResponse(messages, "Session messages retrieved successfully"));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<IEnumerable<ChatMessageDto>>.ErrorResponse(ex.Message));
        }
    }

    [HttpPost("messages")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendMessage([FromBody] SendMessageRequest request)
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

            return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(message, "Message sent successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatMessageDto>.ErrorResponse(ex.Message));
        }
    }

    [HttpPut("sessions/{sessionId}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatSessionDto>>> EndChatSession(int sessionId)
    {
        try
        {
            var session = await _chatService.EndChatSessionAsync(sessionId);

            // Notify both parties that the chat has ended
            if (session.CustomerId > 0)
            {
                await _chatHubContext.Clients.User(session.CustomerId.ToString()).SendAsync("ChatEnded", sessionId);
            }

            if (session.ManagerId.HasValue)
            {
                await _chatHubContext.Clients.User(session.ManagerId.Value.ToString()).SendAsync("ChatEnded", sessionId);
            }

            return Ok(ApiResponse<ChatSessionDto>.SuccessResponse(session, "Chat session ended successfully"));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
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

    [HttpGet("messages/unread/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessageDto>>>> GetUnreadMessages(int userId)
    {
        var messages = await _chatService.GetUnreadMessagesAsync(userId);
        return Ok(ApiResponse<IEnumerable<ChatMessageDto>>.SuccessResponse(messages, "Unread messages retrieved successfully"));
    }

    [HttpGet("sessions/customer/{customerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatSessionDto>>>> GetCustomerChatHistory(int customerId)
    {
        try
        {
            var sessions = await _chatService.GetCustomerChatHistoryAsync(customerId);
            return Ok(ApiResponse<IEnumerable<ChatSessionDto>>.SuccessResponse(sessions, "Customer chat history retrieved successfully"));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<IEnumerable<ChatSessionDto>>.ErrorResponse(ex.Message));
        }
    }
}