/**
 * API Data Structures & Types
 * ===========================
 *
 * This file documents the data structures used in the Referee Assignment system.
 * Use these as reference for your backend API and frontend components.
 */

// ============================================================================
// RefereeAssignment Object
// ============================================================================

/**
 * Main assignment object returned from the backend API
 *
 * @typedef {Object} RefereeAssignment
 * @property {number} id - Unique identifier for the assignment
 * @property {number} refereeId - ID of the referee being assigned
 * @property {number} raceId - ID of the race/match
 * @property {string} matchName - Name/description of the match (e.g., "Horse A vs Horse B")
 * @property {string} dateTime - ISO 8601 date string (e.g., "2026-06-15T14:30:00")
 * @property {string} tournamentName - Name of the tournament
 * @property {string} location - Location of the race (optional)
 * @property {string} status - "pending" | "accepted" | "rejected"
 * @property {Date} createdAt - When the assignment was created
 * @property {Date} respondedAt - When the referee responded (nullable)
 * @property {string} response - "Accept" | "Reject" (nullable until responded)
 * @property {string} notes - Optional notes from organizers
 * @property {RaceDetails} [raceDetails] - Additional race information
 */

const exampleAssignment = {
  id: 1,
  refereeId: 42,
  raceId: 100,
  matchName: "Arabian Cup Final - Heat 1",
  dateTime: "2026-06-15T14:30:00Z",
  tournamentName: "National Championship 2026",
  location: "Hippodrome Central",
  status: "pending",
  createdAt: "2026-06-01T10:00:00Z",
  respondedAt: null,
  response: null,
  notes: "This is an important championship match. Please confirm ASAP.",
  raceDetails: {
    distance: 2000,
    numberOfHorses: 6,
    trackType: "grass",
    weatherConditions: "sunny",
  },
};

// ============================================================================
// AssignmentResponse Object
// ============================================================================

/**
 * Data sent when referee responds to an assignment
 *
 * @typedef {Object} AssignmentResponse
 * @property {number} assignmentId - ID of the assignment being responded to
 * @property {string} response - "Accept" or "Reject"
 * @property {string} [reason] - Optional reason for rejection
 * @property {string} [notes] - Optional additional notes
 */

const exampleResponse = {
  assignmentId: 1,
  response: "Accept", // or "Reject"
  reason: null, // Only if rejecting
  notes: "Will attend. Looking forward to this match.", // Optional
};

// ============================================================================
// AssignmentResponse Result Object
// ============================================================================

/**
 * Response from the API after responding to an assignment
 *
 * @typedef {Object} ResponseResult
 * @property {boolean} success - Whether the response was recorded
 * @property {number} assignmentId - The assignment ID
 * @property {string} status - Updated status
 * @property {string} message - Confirmation message
 * @property {Date} respondedAt - When the response was recorded
 */

const exampleResponseResult = {
  success: true,
  assignmentId: 1,
  status: "accepted",
  message: "Your response has been recorded",
  respondedAt: "2026-06-01T11:30:00Z",
};

// ============================================================================
// API Response Wrapper
// ============================================================================

/**
 * Standard API response format (follows your existing pattern)
 *
 * @typedef {Object} ApiResponse
 * @property {boolean} success - Success indicator
 * @property {any} data - Response data (type depends on endpoint)
 * @property {string} [message] - Optional message
 * @property {any} [error] - Error details if failed
 */

const exampleApiResponse = {
  success: true,
  data: [exampleAssignment],
  message: "Assignments retrieved successfully",
};

// ============================================================================
// Pagination (if needed)
// ============================================================================

/**
 * Paginated API response (for larger datasets)
 *
 * @typedef {Object} PaginatedResponse
 * @property {number} pageNumber - Current page (1-indexed)
 * @property {number} pageSize - Items per page
 * @property {number} totalCount - Total number of items
 * @property {number} totalPages - Total number of pages
 * @property {RefereeAssignment[]} data - Array of assignments
 */

const examplePaginatedResponse = {
  pageNumber: 1,
  pageSize: 10,
  totalCount: 25,
  totalPages: 3,
  data: [exampleAssignment],
};

// ============================================================================
// Backend API Endpoints Reference
// ============================================================================

/**
 * GET /api/referee/assignments/pending
 * Get all pending referee assignments for the current referee
 *
 * @returns {RefereeAssignment[]} Array of pending assignments
 *
 * @example
 * const response = await fetch('/api/referee/assignments/pending', {
 *   method: 'GET',
 *   headers: {
 *     'Authorization': `Bearer ${token}`,
 *     'Content-Type': 'application/json'
 *   }
 * });
 * const assignments = await response.json();
 */

/**
 * GET /api/referee/assignments
 * Get all referee assignments (pending, accepted, rejected)
 *
 * @query {string} [status=pending] - Filter by status
 * @query {number} [pageNumber=1] - Page number for pagination
 * @query {number} [pageSize=10] - Items per page
 * @returns {PaginatedResponse | RefereeAssignment[]}
 */

/**
 * POST /api/referee/assign/respond
 * Submit a response to a referee assignment (Accept or Reject)
 *
 * @body {AssignmentResponse} - Assignment response data
 * @returns {ResponseResult} - Response confirmation
 *
 * @example
 * const response = await fetch('/api/referee/assign/respond', {
 *   method: 'POST',
 *   headers: {
 *     'Authorization': `Bearer ${token}`,
 *     'Content-Type': 'application/json'
 *   },
 *   body: JSON.stringify({
 *     assignmentId: 1,
 *     response: 'Accept',
 *     notes: 'Will attend'
 *   })
 * });
 * const result = await response.json();
 */

/**
 * GET /api/referee/assignments/{id}
 * Get details of a specific assignment
 *
 * @param {number} id - Assignment ID
 * @returns {RefereeAssignment}
 */

/**
 * GET /api/referee/assignments/history
 * Get assignment history (accepted and rejected assignments)
 *
 * @query {number} [pageNumber=1] - Page number
 * @query {number} [pageSize=20] - Items per page
 * @query {string} [status] - Filter by status (accepted/rejected)
 * @returns {PaginatedResponse}
 */

// ============================================================================
// Error Responses
// ============================================================================

/**
 * Standard error response format
 *
 * @typedef {Object} ErrorResponse
 * @property {boolean} success - Always false for errors
 * @property {string} message - Error message
 * @property {string} error - Error code
 * @property {Object} details - Additional error details
 */

const exampleErrorResponse = {
  success: false,
  message: "Assignment not found",
  error: "NOT_FOUND",
  details: {
    assignmentId: 999,
    status: 404,
  },
};

// ============================================================================
// Validation Rules
// ============================================================================

/**
 * AssignmentResponse Validation
 *
 * assignmentId:
 *   - Required: true
 *   - Type: number
 *   - Min: 1
 *
 * response:
 *   - Required: true
 *   - Type: string
 *   - Allowed values: "Accept" | "Reject"
 *
 * reason:
 *   - Required: false
 *   - Type: string
 *   - Max length: 500 characters
 *   - Only shown if response is "Reject"
 *
 * notes:
 *   - Required: false
 *   - Type: string
 *   - Max length: 1000 characters
 */

// ============================================================================
// Status Flow
// ============================================================================

/**
 * Status transitions:
 *
 * pending
 *   ↓ (Accept)
 * accepted ← Final state
 *
 * pending
 *   ↓ (Reject)
 * rejected ← Final state
 *
 * Note: Once an assignment is accepted or rejected, the status cannot be changed.
 */

// ============================================================================
// Timestamps
// ============================================================================

/**
 * All dates are in ISO 8601 format with UTC timezone
 *
 * Format: YYYY-MM-DDTHH:mm:ss.sssZ
 * Example: 2026-06-15T14:30:00.000Z
 *
 * Use JavaScript Date parsing:
 * const date = new Date('2026-06-15T14:30:00.000Z');
 * const formatted = date.toLocaleString('en-US');
 */

// ============================================================================
// Common Scenarios
// ============================================================================

/**
 * Scenario 1: Referee receives and accepts an assignment
 *
 * Request:
 * POST /api/referee/assign/respond
 * {
 *   "assignmentId": 1,
 *   "response": "Accept"
 * }
 *
 * Response:
 * {
 *   "success": true,
 *   "assignmentId": 1,
 *   "status": "accepted",
 *   "message": "Your response has been recorded",
 *   "respondedAt": "2026-06-01T11:30:00Z"
 * }
 */

/**
 * Scenario 2: Referee receives and rejects an assignment with reason
 *
 * Request:
 * POST /api/referee/assign/respond
 * {
 *   "assignmentId": 1,
 *   "response": "Reject",
 *   "reason": "Schedule conflict with another event",
 *   "notes": "I can referee other matches during the tournament"
 * }
 *
 * Response:
 * {
 *   "success": true,
 *   "assignmentId": 1,
 *   "status": "rejected",
 *   "message": "Your rejection has been recorded",
 *   "respondedAt": "2026-06-01T11:35:00Z"
 * }
 */

export const API_TYPES = {
  RefereeAssignment: exampleAssignment,
  AssignmentResponse: exampleResponse,
  ResponseResult: exampleResponseResult,
  ApiResponse: exampleApiResponse,
  PaginatedResponse: examplePaginatedResponse,
  ErrorResponse: exampleErrorResponse,
};
