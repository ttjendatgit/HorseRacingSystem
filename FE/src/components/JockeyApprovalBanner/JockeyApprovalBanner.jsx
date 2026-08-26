import "./JockeyApprovalBanner.css";

// J-UX: shared presentational banner for Jockey competitive-approval state (Pending/Approved/
// Rejected). Callers own the exact copy (title/description) since it differs by page context;
// this only owns the tone-based layout so it isn't re-implemented per page.
function JockeyApprovalBanner({ tone, title, description, compact = false }) {
  if (!tone || tone === "unknown") return null;

  if (compact) {
    return (
      <div className={`jab jab--${tone} jab--compact`} role="status">
        <span className="jab__dot" />
        <span className="jab__title">{title}</span>
      </div>
    );
  }

  return (
    <div className={`jab jab--${tone}`} role="status">
      <div className="jab__title">{title}</div>
      {description && <p className="jab__text">{description}</p>}
    </div>
  );
}

export default JockeyApprovalBanner;
