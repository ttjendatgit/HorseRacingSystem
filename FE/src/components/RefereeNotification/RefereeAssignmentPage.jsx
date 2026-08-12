import { useState } from "react";
import { RefereeNotificationsPanel } from "./RefereeNotificationsPanel";
import "../../pages/RefereeSharedLayout.css";
import "./RefereeAssignmentPage.css";

export function RefereeAssignmentPage() {
  const [expandDetails, setExpandDetails] = useState(false);

  return (
    <div className="referee-page referee-assignments">
      <div className="referee-layout">
        <aside className="referee-sidebar">
          <div className="referee-sidebar__header">
            <p className="pill">Bảng trọng tài</p>
            <h3>Phân công trận đấu</h3>
            <p className="muted">Xem xét nhiệm vụ sắp tới.</p>
          </div>
          <div className="referee-sidebar__card">
            <p className="muted">Đang chờ phản hồi</p>
            <h4>Phân công đang hoạt động</h4>
            <span>Kiểm tra phân công bên dưới</span>
          </div>
        </aside>

        <div className="referee-content">
          <section className="referee-hero">
            <div>
              <span className="pill">Cần hành động</span>
              <h1>Phân công trận đấu</h1>
              <p>
                Xem xét và phản hồi phân công trọng tài từ ban tổ chức giải đấu.
                Chấp nhận hoặc từ chối nhiệm vụ dựa trên lịch trình của bạn.
              </p>
              <div className="referee-hero__actions">
                <button
                  className="primary-button"
                  onClick={() => setExpandDetails(!expandDetails)}
                >
                  {expandDetails ? "Ẩn" : "Hiện"} Hướng dẫn
                </button>
              </div>
            </div>
            <div className="referee-hero__panel">
              <span>Vai trò của bạn</span>
              <strong>Trọng tài</strong>
            </div>
          </section>

          <section className="referee-section">
            <div className="section-heading">
              <h2>Phân công đang chờ</h2>
              <p>Phản hồi các trận đấu và giải đấu được chỉ định bên dưới.</p>
            </div>
            <RefereeNotificationsPanel />
          </section>

          {expandDetails && (
            <section className="referee-section guidelines-section">
              <h2>Hướng dẫn trọng tài</h2>
              <div className="guidelines-grid">
                <div className="guideline-card">
                  <h4>Trách nhiệm</h4>
                  <ul>
                    <li>Giám sát cuộc đua về vi phạm quy tắc</li>
                    <li>Ghi nhận dữ liệu thời gian và vị trí</li>
                    <li>Ghi nhận mọi sự cố hoặc khiếu nại</li>
                    <li>Nộp báo cáo cuộc đua chính thức</li>
                  </ul>
                </div>

                <div className="guideline-card">
                  <h4>Trước trận đấu</h4>
                  <ul>
                    <li>Đến sớm 30 phút</li>
                    <li>Xem xét hồ sơ ngựa và nài</li>
                    <li>Kiểm tra điều kiện đường đua</li>
                    <li>Tham dự họp tóm tắt bắt buộc</li>
                  </ul>
                </div>

                <div className="guideline-card">
                  <h4>Trong trận đấu</h4>
                  <ul>
                    <li>Đứng vị trí quan sát tối ưu</li>
                    <li>Giám sát vi phạm quy tắc</li>
                    <li>Ghi nhận kết quả theo thời gian thực</li>
                    <li>Sẵn sàng giải đáp</li>
                  </ul>
                </div>

                <div className="guideline-card">
                  <h4>Sau trận đấu</h4>
                  <ul>
                    <li>Ghi nhận mọi sự cố</li>
                    <li>Nộp báo cáo chính thức</li>
                    <li>Thông báo vấn đề cho ban tổ chức</li>
                    <li>Lưu trữ tất cả tài liệu</li>
                  </ul>
                </div>
              </div>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
