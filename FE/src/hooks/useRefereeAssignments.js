import { useState, useEffect, useCallback } from "react";
import { getPendingRefereeAssignments } from "../services/refereeAssignmentApi";

/**
 * Custom Hook: useRefereeAssignments
 * Manages loading, fetching, and displaying referee assignment notifications
 *
 * @param {boolean} autoFetch - Whether to automatically fetch assignments on mount
 * @returns {Object} - { assignments, isLoading, error, refreshAssignments, removeAssignment, updateAssignment }
 */
export function useRefereeAssignments(autoFetch = true) {
  const [assignments, setAssignments] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState(null);

  /**
   * Fetch pending referee assignments from API
   */
  const refreshAssignments = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const data = await getPendingRefereeAssignments();
      setAssignments(Array.isArray(data) ? data : []);
    } catch (err) {
      console.error("Error fetching assignments:", err);
      setError(err.message || "Failed to load assignments");
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Remove assignment from the list (after user responds)
   */
  const removeAssignment = useCallback((assignmentId) => {
    setAssignments((prev) => prev.filter((a) => a.id !== assignmentId));
  }, []);

  /**
   * Update assignment status without removing it
   */
  const updateAssignment = useCallback((assignmentId, updates) => {
    setAssignments((prev) =>
      prev.map((a) => (a.id === assignmentId ? { ...a, ...updates } : a))
    );
  }, []);

  /**
   * Fetch assignments on component mount
   */
  useEffect(() => {
    if (autoFetch) {
      refreshAssignments();
    }
  }, [autoFetch, refreshAssignments]);

  return {
    assignments,
    isLoading,
    error,
    refreshAssignments,
    removeAssignment,
    updateAssignment,
  };
}

export default useRefereeAssignments;
