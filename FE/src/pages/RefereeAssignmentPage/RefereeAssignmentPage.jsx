import { useCallback, useMemo, useState, useEffect } from "react";
import {
  RaceButton,
  RaceDataRow,
  RaceEmptyState,
  RacePanel,
  RaceStatusBadge,
  RaceTabs,
} from "../../components/ui/RaceUi";
import { getMyAssignments, respondToRefereeAssignment } from "../../services/refereeAssignmentApi";
import {
  ASSIGNMENT_TABS,
  filterAssignmentsByTab,
  getAssignmentId,
  getAssignmentStatus,
  getAssignmentStatusDetails,
  getAssignmentTabCounts,
  getDefaultAssignmentTab,
  isPendingAssignment,
} from "../../utils/refereeAssignmentDisplay";
import "./RefereeAssignmentPage.css";

const roleLabels = {
  "Chief Referee": "Trọng tài trưởng",
  Assistant: "Trợ lý",
};

const tabCopy = {
  pending: {
    title: "Chờ phản hồi",
    description: "Ưu tiên các phân công cần xác nhận hoặc từ chối.",
    emptyTitle: "Không có phân công chờ xử lý",
    emptyDescription: "Các phân công đã xử lý nằm ở những tab còn lại.",
  },
  confirmed: {
    title: "Đã xác nhận",
    description: "Các phân công đã được chấp nhận hoặc đã hoàn thành.",
    emptyTitle: "Chưa có phân công đã xác nhận",
    emptyDescription: "Khi bạn xác nhận lời mời, phân công sẽ xuất hiện tại đây.",
  },
  rejected: {
    title: "Đã từ chối",
    description: "Các phân công đã được phản hồi từ chối.",
    emptyTitle: "Chưa có phân công bị từ chối",
    emptyDescription: "Không có lời mời nào bị từ chối trong danh sách hiện tại.",
  },
  all: {
    title: "Tất cả phân công",
    description: "Toàn bộ phân công hiện có của tài khoản trọng tài.",
    emptyTitle: "Chưa có phân công",
    emptyDescription: "Bạn chưa được phân công cuộc đua nào. Vui lòng quay lại sau.",
  },
};

function readAssignmentValue(assignment, camelKey, pascalKey) {
  return assignment?.[camelKey] ?? assignment?.[pascalKey];
}

function formatDateTime(value) {
  if (!value) return "Chưa xác định";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Chưa xác định";
  return date.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getScheduledAt(assignment) {
  return (
    readAssignmentValue(assignment, "raceDate", "RaceDate") ??
    readAssignmentValue(assignment, "scheduledAt", "ScheduledAt") ??
    readAssignmentValue(assignment, "scheduledStartDate", "ScheduledStartDate")
  );
}

function getDisplayRole(role) {
  return roleLabels[role] || role || "Trọng tài";
}

function withAssignmentStatus(assignment, status) {
  if (
    Object.prototype.hasOwnProperty.call(assignment, "Status") &&
    !Object.prototype.hasOwnProperty.call(assignment, "status")
  ) {
    return { ...assignment, Status: status };
  }
  return { ...assignment, status };
}

export default function RefereeAssignmentPage() {
  const [assignments, setAssignments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(null);
  const [error, setError] = useState("");
  const [toast, setToast] = useState(null);
  const [activeTab, setActiveTab] = useState(null);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      try {
        const data = await getMyAssignments();
        if (!cancelled) {
          const list = Array.isArray(data?.data)
            ? data.data
            : Array.isArray(data)
              ? data
              : [];
          setAssignments(list);
          setError("");
        }
      } catch (e) {
        if (!cancelled) {
          setError(e?.message || "Lỗi không xác định");
          setAssignments([]);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => {
      cancelled = true;
    };
  }, []);

  const showToast = useCallback((message) => {
    setToast(message);
    window.setTimeout(() => setToast(null), 3500);
  }, []);

  const handleRespond = useCallback(
    async (assignmentId, accept) => {
      const id = String(assignmentId || "");
      if (!id) return;
      const action = accept ? "accept" : "reject";
      setActionLoading({ id, action });
      try {
        await respondToRefereeAssignment(id, accept ? "Accept" : "Reject");
        setAssignments((prev) =>
          prev.map((assignment) =>
            String(getAssignmentId(assignment)) === id
              ? withAssignmentStatus(assignment, accept ? "Confirmed" : "Cancelled")
              : assignment
          )
        );
        showToast(accept ? "Đã xác nhận phân công" : "Đã từ chối phân công");
      } catch {
        showToast("Không thể cập nhật. Vui lòng thử lại.");
      } finally {
        setActionLoading(null);
      }
    },
    [showToast]
  );

  const counts = useMemo(() => getAssignmentTabCounts(assignments), [assignments]);
  const selectedTab = activeTab || getDefaultAssignmentTab(counts);
  const visibleAssignments = useMemo(
    () => filterAssignmentsByTab(assignments, selectedTab),
    [assignments, selectedTab]
  );
  const tabs = useMemo(
    () => ASSIGNMENT_TABS.map((tab) => ({ ...tab, count: counts[tab.value] })),
    [counts]
  );
  const currentCopy = tabCopy[selectedTab] || tabCopy.all;

  const renderActions = (assignment) => {
    const assignmentId = getAssignmentId(assignment);
    const status = getAssignmentStatus(assignment);
    if (!isPendingAssignment(status)) return null;

    const id = String(assignmentId || "");
    const rowBusy = actionLoading?.id === id;
    return (
      <>
        <RaceButton
          size="compact"
          loading={rowBusy && actionLoading?.action === "accept"}
          disabled={rowBusy}
          onClick={() => handleRespond(id, true)}
        >
          Xác nhận
        </RaceButton>
        <RaceButton
          variant="danger"
          size="compact"
          loading={rowBusy && actionLoading?.action === "reject"}
          disabled={rowBusy}
          onClick={() => handleRespond(id, false)}
        >
          Từ chối
        </RaceButton>
      </>
    );
  };

  const renderRow = (assignment, index) => {
    const id = getAssignmentId(assignment) || index;
    const status = getAssignmentStatus(assignment);
    const statusDetails = getAssignmentStatusDetails(status);
    const raceName = readAssignmentValue(assignment, "raceName", "RaceName") || "Cuộc đua";
    const tournamentName =
      readAssignmentValue(assignment, "tournamentName", "TournamentName") || "Chưa xác định giải đấu";
    const role = getDisplayRole(readAssignmentValue(assignment, "role", "Role"));
    const scheduledAt = getScheduledAt(assignment);
    const assignedAt = readAssignmentValue(assignment, "assignedAt", "AssignedAt");
    const raceStatus = readAssignmentValue(assignment, "raceStatus", "RaceStatus");
    const resultStatus = readAssignmentValue(assignment, "resultStatus", "ResultStatus");

    const secondaryMeta = [
      { label: "Phân công", value: formatDateTime(assignedAt) },
      raceStatus ? { label: "Cuộc đua", value: raceStatus } : null,
      resultStatus ? { label: "Kết quả", value: resultStatus } : null,
    ].filter(Boolean);

    return (
      <RaceDataRow
        key={id}
        title={raceName}
        subtitle={tournamentName}
        badge={<RaceStatusBadge variant={statusDetails.variant}>{statusDetails.label}</RaceStatusBadge>}
        meta={[
          { label: "Lịch", value: formatDateTime(scheduledAt) },
          { label: "Vai trò", value: role },
        ]}
        secondaryMeta={secondaryMeta}
        actions={renderActions(assignment)}
      />
    );
  };

  return (
    <main className="ra-page">
      <div className="ra-shell">
        <header className="ra-header">
          <h1>Phân công trọng tài</h1>
          <p>Theo dõi lời mời và phản hồi phân công từ ban tổ chức.</p>
        </header>

        <div className="ra-toolbar">
          <RaceTabs
            tabs={tabs}
            activeValue={selectedTab}
            onChange={setActiveTab}
            ariaLabel="Lọc phân công trọng tài"
            idPrefix="ra-assignment-tab"
            panelId="ra-assignment-panel"
          />
        </div>

        {error && (
          <div className="ra-alert" role="alert">
            Không thể tải dữ liệu từ máy chủ: {error}
          </div>
        )}

        <RacePanel
          id="ra-assignment-panel"
          role="tabpanel"
          aria-labelledby={`ra-assignment-tab-${selectedTab}`}
          title={currentCopy.title}
          description={currentCopy.description}
          aside={`${visibleAssignments.length}/${counts.all} phân công`}
          className="ra-assignment-panel"
        >
          {loading ? (
            <div className="ra-loading" aria-label="Đang tải phân công">
              <div className="ra-skeleton" />
              <div className="ra-skeleton" />
              <div className="ra-skeleton" />
            </div>
          ) : visibleAssignments.length === 0 ? (
            <RaceEmptyState
              title={currentCopy.emptyTitle}
              description={currentCopy.emptyDescription}
            />
          ) : (
            <div className="ra-list">
              {visibleAssignments.map(renderRow)}
            </div>
          )}
        </RacePanel>

        {toast && (
          <div className="ra-toast" role="status" aria-live="polite">
            {toast}
          </div>
        )}
      </div>
    </main>
  );
}
