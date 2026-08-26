import { useRef, useState } from "react";
import { uploadRaceComplaintEvidence } from "../services/managementApi";
import { EVIDENCE_ACCEPT_ATTR, MAX_EVIDENCE_PER_SOURCE, validateEvidenceFile } from "../utils/raceComplaintDisplay";

// COMPLAINT-EVIDENCE-V1: attaches one or more image/video files to an EXISTING complaint
// (uploads immediately on selection, one file at a time). For the filing-time case — no
// complaintId yet — callers instead collect File objects locally and upload them in a loop
// right after createRaceComplaint() resolves; see OwnerParticipationsPage/JockeyProfilePage.
//
// COMPLAINT-EVIDENCE-V1.1: currentCount shows "N/5" and locally disables the picker at the cap —
// a client-side convenience only, the backend re-enforces the same limit per EvidenceSource.
export default function ComplaintEvidenceUploader({ complaintId, onUploaded, label = "Thêm bằng chứng", currentCount = 0 }) {
  const inputRef = useRef(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const atLimit = currentCount >= MAX_EVIDENCE_PER_SOURCE;

  const handleFiles = async (fileList) => {
    const files = Array.from(fileList || []);
    if (files.length === 0) return;

    setError("");
    setBusy(true);
    try {
      for (const file of files) {
        const check = validateEvidenceFile(file);
        if (!check.valid) {
          setError(check.error);
          continue;
        }
        try {
          await uploadRaceComplaintEvidence(complaintId, file);
        } catch (e) {
          setError(e?.message || `Tải lên thất bại: ${file.name}`);
        }
      }
      onUploaded?.();
    } finally {
      setBusy(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const disabled = busy || atLimit;

  return (
    <div style={{ display: "grid", gap: 4 }}>
      <label style={{ display: "inline-flex" }}>
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={EVIDENCE_ACCEPT_ATTR}
          disabled={disabled}
          onChange={(e) => handleFiles(e.target.files)}
          style={{ display: "none" }}
        />
        <span
          style={{
            padding: "6px 12px",
            fontSize: 12,
            borderRadius: 6,
            border: "1px solid var(--hr-border-soft)",
            background: "transparent",
            color: "var(--hr-text)",
            cursor: disabled ? "not-allowed" : "pointer",
            opacity: disabled ? 0.6 : 1,
          }}
          onClick={(e) => { if (disabled) e.preventDefault(); }}
        >
          {busy ? "Đang tải lên..." : `${label} (${currentCount}/${MAX_EVIDENCE_PER_SOURCE})`}
        </span>
      </label>
      {error && <span style={{ fontSize: 11, color: "var(--hr-danger)" }}>{error}</span>}
    </div>
  );
}
