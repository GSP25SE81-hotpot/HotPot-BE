﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Chat;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces.ChatService;
using Capstone.HPTY.ServiceLayer.Interfaces.UserService;
using Capstone.HPTY.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Capstone.HPTY.API.Controllers
{
    [Route("api/chat")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IChatSignalRService _signalRService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            ICurrentUserService currentUserService,
            IChatSignalRService signalRService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _currentUserService = currentUserService;
            _signalRService = signalRService;
            _logger = logger;
        }

        [HttpGet("sessions/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ChatSessionDetailDto>>> GetChatSession(int sessionId)
        {
            try
            {
                var session = await _chatService.GetChatSessionAsync(sessionId);
                if (session == null)
                    return NotFound(ApiResponse<ChatSessionDetailDto>.ErrorResponse($"Chat session not found"));

                return Ok(ApiResponse<ChatSessionDetailDto>.SuccessResponse(session, "Chat session retrieved"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ChatSessionDetailDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpPost("customer/sessions")]
        [Authorize(Roles = "Customer")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ChatSessionDto>>> CreateChatSession([FromBody] CreateChatSessionRequest request)
        {
            try
            {
                // Get current user ID
                var customerId = _currentUserService.GetUserId();

                // Create chat session without assigning a manager
                var session = await _chatService.CreateChatSessionAsync(customerId, request.Topic);

                // Notify managers about new unassigned chat
                await _signalRService.NotifyNewChat(session);

                return CreatedAtAction(nameof(GetChatSession), new { sessionId = session.ChatSessionId },
                    ApiResponse<ChatSessionDto>.SuccessResponse(session, "Chat session created"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat session");
                return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpPost("manager/sessions/{sessionId}/join")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ChatSessionDto>>> JoinChatSession(int sessionId)
        {
            try
            {
                // Get current manager ID
                var managerId = _currentUserService.GetUserId();

                // Join the chat session
                var session = await _chatService.JoinChatSessionAsync(sessionId, managerId);

                // Notify customer that a manager has joined
                await _signalRService.NotifyChatAccepted(session);

                return Ok(ApiResponse<ChatSessionDto>.SuccessResponse(session, "Joined chat session"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpPost("messages")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                // Get current user ID
                var senderId = _currentUserService.GetUserId();

                var message = await _chatService.SaveMessageAsync(senderId, request.ChatSessionId, request.Message);

                // Notify receiver about new message
                await _signalRService.NotifyNewMessage(message, request.ChatSessionId);

                return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(message, "Message sent"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ChatMessageDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("messages/session/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessageDto>>>> GetSessionMessages(
            int sessionId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var messages = await _chatService.GetSessionMessagesAsync(sessionId, pageNumber, pageSize);
                return Ok(ApiResponse<IEnumerable<ChatMessageDto>>.SuccessResponse(messages, "Messages retrieved"));
            }
            catch (NotFoundException ex)
            {
                return NotFound(ApiResponse<IEnumerable<ChatMessageDto>>.ErrorResponse(ex.Message));
            }
        }

        [HttpPut("sessions/{sessionId}/end")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ChatSessionDto>>> EndChatSession(int sessionId)
        {
            try
            {
                var session = await _chatService.EndChatSessionAsync(sessionId);

                // Notify both parties that chat has ended
                await _signalRService.NotifyChatEnded(session);

                return Ok(ApiResponse<ChatSessionDto>.SuccessResponse(session, "Chat ended"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("sessions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ChatSessionDto>>>> GetUserSessions([FromQuery] bool activeOnly = false)
        {
            try
            {
                // Get current user ID
                var userId = _currentUserService.GetUserId();

                var sessions = await _chatService.GetUserChatSessionsAsync(userId, activeOnly);
                return Ok(ApiResponse<IEnumerable<ChatSessionDto>>.SuccessResponse(sessions, "Chat sessions retrieved"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<IEnumerable<ChatSessionDto>>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("manager/sessions/unassigned")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ChatSessionDto>>>> GetUnassignedSessions()
        {
            try
            {
                var sessions = await _chatService.GetUnassignedChatSessionsAsync();
                return Ok(ApiResponse<IEnumerable<ChatSessionDto>>.SuccessResponse(sessions, "Unassigned chat sessions retrieved"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<IEnumerable<ChatSessionDto>>.ErrorResponse(ex.Message));
            }
        }
    }
}