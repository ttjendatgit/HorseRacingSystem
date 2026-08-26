import { useState } from "react";
import { deleteRaceComplaintEvidence } from "../services/managementApi";
import {
  MAX_EVIDENCE_PER_SOURCE,
  canFilerMutateEvidence,
  canRefereeUploadEvidence,
  groupComplaintEvidenceByUploader,
  normalizeEvidenceMediaType,
} from "../utils/raceComplaintDisplay";

const thumbBoxStyle = {
  width: 72,
  height: 72,
  borderRadius: 8,
  overflow: "hidden",
  border: "1px solid var(--hr-border-soft)",
  background: "var(--hr-surface-2)",
  display: "block",
};

function EvidenceThumb({ item }) {
  const url = item.fileUrl ?? item.FileUrl;
  const mediaType = normalizeEvidenceMediaType(item.mediaType ?? item.MediaType);
  const fileName = item.fileName ?? item.FileName ?? "evidence";
  return (
    <a
      href={url}
      target="_blank"
      rel="noreferrer"
      title={fileName}
      style={thumbBoxStyle}
    >
      {mediaType === "Video" ? (
        <video src={url} muted preload="metadata" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      ) : (
        <img src={url} alt={fileName} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      )}
    </a>
  );
}

function EvidenceGroup({ title, items, mutable, complaintId, onDeleted }) {
  const [removingId, setRemovingId] = useState(null);
  const [error, setError] = useState("");

  const remove = async (item) => {
    const id = item.id ?? item.Id;
    setError("");
    setRemovingId(id);
    try {
      await deleteRaceComplaintEvidence(complaintId, id);
      onDeleted?.();
    } catch (e) {
      setError(e?.message || "Xóa bằng chứng thất bại.");
    } finally {
      setRemovingId(null);
    }
  };

  return (
    <div>
      <p style={{ margin: "0 0 6px", fontSize: 11, fontWeight: 700, color: "var(--hr-muted)", textTransform: "uppercase", letterSpacing: 0.4 }}>
        {title} ({items.length}/{MAX_EVIDENCE_PER_SOURCE})
      </p>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        {items.map((item) => {
          const id = item.id ?? item.Id;
          return (
            <div key={id} style={{ position: "relative" }}>
              <EvidenceThumb item={item} />
              {mutable && (
                <button
                  type="button"
                  aria-label="Xóa bằng chứng"
                  title="Xóa bằng chứng"
                  disabled={removingId === id}
                  onClick={() => remove(item)}
                  style={{
                    position: "absolute",
                    top: -6,
                    right: -6,
                    width: 20,
                    height: 20,
                    lineHeight: "18px",
                    borderRadius: "50%",
                    border: "1px solid var(--hr-border-soft)",
                    background: "var(--hr-surface, #fff)",
                    color: "var(--hr-danger)",
                    fontSize: 12,
                    cursor: removingId === id ? "not-allowed" : "pointer",
                    opacity: removingId === id ? 0.6 : 1,
                    padding: 0,
                  }}
                >
                  ×
                </button>
              )}
            </div>
          );
        })}
      </div>
      {error && <span style={{ fontSize: 11, color: "var(--hr-danger)" }}>{error}</span>}
    </div>
  );
}

// COMPLAINT-EVIDENCE-V1: renders one complaint's Evidence list grouped by who uploaded it — the
// filer's evidence and the assigned Referee's evidence stay visually separate so Admin can
// actually "review both sides" rather than scan one mixed pile.
//
// COMPLAINT-EVIDENCE-V1.1: viewerRole ("filer" | "referee", omitted = read-only e.g. Admin) plus
// complaintId/complaintStatus/onDeleted turn on remove buttons for the viewer's OWN side only,
// gated by the exact same mutation windows as upload (canFilerMutateEvidence / canRefereeUploadEvidence)
// — the Referee side becomes read-only immediately once a response has been submitted.
export default function ComplaintEvidenceGallery({ evidence, complaintId, complaintStatus, viewerRole, onDeleted }) {
  const { filerEvidence, refereeEvidence, otherEvidence } = groupComplaintEvidenceByUploader(evidence);
  const total = filerEvidence.length + refereeEvidence.length + otherEvidence.length;
  if (total === 0) return null;

  const filerMutable = viewerRole === "filer" && canFilerMutateEvidence(complaintStatus);
  const refereeMutable = viewerRole === "referee" && canRefereeUploadEvidence(complaintStatus);

  return (
    <div style={{ display: "grid", gap: 10, marginTop: 8 }}>
      {filerEvidence.length > 0 && (
        <EvidenceGroup title="Bằng chứng của người khiếu nại" items={filerEvidence} mutable={filerMutable} complaintId={complaintId} onDeleted={onDeleted} />
      )}
      {refereeEvidence.length > 0 && (
        <EvidenceGroup title="Bằng chứng của trọng tài" items={refereeEvidence} mutable={refereeMutable} complaintId={complaintId} onDeleted={onDeleted} />
      )}
      {otherEvidence.length > 0 && <EvidenceGroup title="Bằng chứng khác" items={otherEvidence} mutable={false} complaintId={complaintId} onDeleted={onDeleted} />}
    </div>
  );
}
