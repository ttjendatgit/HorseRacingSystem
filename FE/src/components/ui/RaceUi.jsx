import { useId } from "react";
import "./RaceUi.css";

function cx(...parts) {
  return parts.filter(Boolean).join(" ");
}

function fieldDescriptionIds(fieldId, helpText, errorText) {
  return [
    helpText ? `${fieldId}-help` : "",
    errorText ? `${fieldId}-error` : "",
  ].filter(Boolean).join(" ") || undefined;
}

export function RaceButton({
  children,
  variant = "primary",
  size = "default",
  loading = false,
  disabled = false,
  className = "",
  type = "button",
  ...props
}) {
  return (
    <button
      type={type}
      className={cx("rm-button", `rm-button--${variant}`, `rm-button--${size}`, className)}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...props}
    >
      {loading && <span className="rm-button__spinner" aria-hidden="true" />}
      <span className="rm-button__label">{children}</span>
    </button>
  );
}

export function RaceField({
  id,
  label,
  helpText,
  errorText,
  className = "",
  inputClassName = "",
  ...props
}) {
  const generatedId = useId();
  const fieldId = id || generatedId;
  const describedBy = fieldDescriptionIds(fieldId, helpText, errorText);

  return (
    <div className={cx("rm-field", className)}>
      {label && (
        <label className="rm-field__label" htmlFor={fieldId}>
          {label}
        </label>
      )}
      <input
        id={fieldId}
        className={cx("rm-control", errorText && "rm-control--invalid", inputClassName)}
        aria-invalid={errorText ? "true" : undefined}
        aria-describedby={describedBy}
        {...props}
      />
      {errorText ? (
        <p id={`${fieldId}-error`} className="rm-field__message rm-field__message--error">
          {errorText}
        </p>
      ) : helpText ? (
        <p id={`${fieldId}-help`} className="rm-field__message">
          {helpText}
        </p>
      ) : null}
    </div>
  );
}

export function RaceSelect({
  id,
  label,
  helpText,
  errorText,
  options = [],
  className = "",
  selectClassName = "",
  children,
  ...props
}) {
  const generatedId = useId();
  const fieldId = id || generatedId;
  const describedBy = fieldDescriptionIds(fieldId, helpText, errorText);

  return (
    <div className={cx("rm-field", className)}>
      {label && (
        <label className="rm-field__label" htmlFor={fieldId}>
          {label}
        </label>
      )}
      <select
        id={fieldId}
        className={cx("rm-control", "rm-select", errorText && "rm-control--invalid", selectClassName)}
        aria-invalid={errorText ? "true" : undefined}
        aria-describedby={describedBy}
        {...props}
      >
        {children || options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {errorText ? (
        <p id={`${fieldId}-error`} className="rm-field__message rm-field__message--error">
          {errorText}
        </p>
      ) : helpText ? (
        <p id={`${fieldId}-help`} className="rm-field__message">
          {helpText}
        </p>
      ) : null}
    </div>
  );
}

export function RaceStatusBadge({ children, variant = "neutral", className = "" }) {
  return (
    <span className={cx("rm-status", `rm-status--${variant}`, className)}>
      {children || "Không xác định"}
    </span>
  );
}

export function RaceTabs({
  tabs,
  activeValue,
  onChange,
  ariaLabel,
  className = "",
  idPrefix = "rm-tab",
  panelId,
}) {
  const moveFocus = (event, nextValue) => {
    onChange(nextValue);
    window.requestAnimationFrame(() => {
      event.currentTarget
        .closest("[role='tablist']")
        ?.querySelector(`[data-rm-tab="${nextValue}"]`)
        ?.focus();
    });
  };

  const onKeyDown = (event, value) => {
    const currentIndex = tabs.findIndex((tab) => tab.value === value);
    if (currentIndex < 0) return;

    if (event.key === "ArrowRight" || event.key === "ArrowDown") {
      event.preventDefault();
      moveFocus(event, tabs[(currentIndex + 1) % tabs.length].value);
    } else if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
      event.preventDefault();
      moveFocus(event, tabs[(currentIndex - 1 + tabs.length) % tabs.length].value);
    } else if (event.key === "Home") {
      event.preventDefault();
      moveFocus(event, tabs[0].value);
    } else if (event.key === "End") {
      event.preventDefault();
      moveFocus(event, tabs[tabs.length - 1].value);
    }
  };

  return (
    <div className={cx("rm-tabs", className)} role="tablist" aria-label={ariaLabel}>
      {tabs.map((tab) => {
        const active = tab.value === activeValue;
        return (
          <button
            key={tab.value}
            id={`${idPrefix}-${tab.value}`}
            type="button"
            role="tab"
            aria-selected={active}
            aria-controls={panelId}
            tabIndex={active ? 0 : -1}
            data-rm-tab={tab.value}
            className={cx("rm-tab", active && "rm-tab--active")}
            onClick={() => onChange(tab.value)}
            onKeyDown={(event) => onKeyDown(event, tab.value)}
          >
            <span className="rm-tab__label">{tab.label}</span>
            {Number.isFinite(tab.count) && (
              <span className="rm-tab__count" aria-label={`${tab.count} mục`}>
                {tab.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

export function RacePanel({
  title,
  description,
  aside,
  children,
  className = "",
  ...props
}) {
  return (
    <section className={cx("rm-panel", className)} {...props}>
      {(title || description || aside) && (
        <div className="rm-panel__head">
          <div>
            {title && <h2>{title}</h2>}
            {description && <p>{description}</p>}
          </div>
          {aside && <div className="rm-panel__aside">{aside}</div>}
        </div>
      )}
      {children}
    </section>
  );
}

export function RaceDataRow({
  title,
  subtitle,
  badge,
  meta = [],
  secondaryMeta = [],
  actions,
  children,
  className = "",
}) {
  return (
    <article className={cx("rm-data-row", className)}>
      <div className="rm-data-row__main">
        <div className="rm-data-row__title-line">
          <h3 className="rm-data-row__title">{title}</h3>
          {badge}
        </div>
        {subtitle && <p className="rm-data-row__subtitle">{subtitle}</p>}
        {children}
      </div>
      {(meta.length > 0 || secondaryMeta.length > 0) && (
        <div className="rm-data-row__details">
          {meta.length > 0 && (
            <dl className="rm-data-row__meta">
              {meta.map((item) => (
                <div key={`${item.label}-${item.value}`} className="rm-data-row__meta-item">
                  <dt>{item.label}</dt>
                  <dd>{item.value || "-"}</dd>
                </div>
              ))}
            </dl>
          )}
          {secondaryMeta.length > 0 && (
            <dl className="rm-data-row__meta rm-data-row__meta--secondary">
              {secondaryMeta.map((item) => (
                <div key={`${item.label}-${item.value}`} className="rm-data-row__meta-item">
                  <dt>{item.label}</dt>
                  <dd>{item.value || "-"}</dd>
                </div>
              ))}
            </dl>
          )}
        </div>
      )}
      {actions && <div className="rm-data-row__actions">{actions}</div>}
    </article>
  );
}

export function RaceEmptyState({ title, description, action, icon, className = "" }) {
  return (
    <div className={cx("rm-empty", className)}>
      {icon && <div className="rm-empty__icon" aria-hidden="true">{icon}</div>}
      <div className="rm-empty__copy">
        <strong>{title}</strong>
        {description && <span>{description}</span>}
      </div>
      {action && <div className="rm-empty__action">{action}</div>}
    </div>
  );
}

export function RaceModalShell({
  title,
  description,
  children,
  footer,
  onClose,
  className = "",
}) {
  const titleId = useId();
  const descriptionId = useId();

  return (
    <div className="rm-modal-backdrop" role="presentation" onClick={onClose}>
      <section
        className={cx("rm-modal", className)}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descriptionId : undefined}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="rm-modal__head">
          <div>
            <h2 id={titleId}>{title}</h2>
            {description && <p id={descriptionId}>{description}</p>}
          </div>
          {onClose && (
            <RaceButton variant="ghost" size="compact" onClick={onClose}>
              Đóng
            </RaceButton>
          )}
        </header>
        <div className="rm-modal__body">{children}</div>
        {footer && <footer className="rm-modal__footer">{footer}</footer>}
      </section>
    </div>
  );
}
