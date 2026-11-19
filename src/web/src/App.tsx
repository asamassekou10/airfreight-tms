import './App.css'

const modules = [
  {
    number: '01',
    name: 'Master data',
    description: 'Customers, parties, airports, airlines, agents, and Incoterms.',
    status: 'First build slice',
  },
  {
    number: '02',
    name: 'Shipment desk',
    description: 'Shipment parties, route legs, cargo, validation, and milestones.',
    status: 'Planned',
  },
  {
    number: '03',
    name: 'Air waybills',
    description: 'House and master AWBs, document versions, and partner exchange.',
    status: 'Planned',
  },
]

function App() {
  return (
    <div className="app-shell">
      <aside className="sidebar" aria-label="Main navigation">
        <a className="brand" href="/" aria-label="Airfreight TMS home">
          <span className="brand-mark" aria-hidden="true">A</span>
          <span className="brand-copy">
            <strong>Airfreight</strong>
            <span>TRANSPORT SYSTEM</span>
          </span>
        </a>

        <div className="nav-label">WORKSPACE</div>
        <nav className="nav-list">
          <a className="nav-item active" href="#overview" aria-current="page">
            <span className="nav-icon" aria-hidden="true">◫</span>
            Overview
          </a>
          <span className="nav-item disabled" aria-disabled="true">
            <span className="nav-icon" aria-hidden="true">⇄</span>
            Shipments
            <span className="nav-soon">Soon</span>
          </span>
          <span className="nav-item disabled" aria-disabled="true">
            <span className="nav-icon" aria-hidden="true">▦</span>
            Master data
            <span className="nav-soon">Soon</span>
          </span>
          <span className="nav-item disabled" aria-disabled="true">
            <span className="nav-icon" aria-hidden="true">▤</span>
            Air waybills
            <span className="nav-soon">Soon</span>
          </span>
          <span className="nav-item disabled" aria-disabled="true">
            <span className="nav-icon" aria-hidden="true">◇</span>
            Finance
            <span className="nav-soon">Soon</span>
          </span>
        </nav>

        <div className="sidebar-footer">
          <span className="environment-dot" aria-hidden="true" />
          <div>
            <strong>Local development</strong>
            <span>Foundation scaffold</span>
          </div>
        </div>
      </aside>

      <main className="main-content" id="overview">
        <header className="topbar">
          <div className="breadcrumb">Workspace <span>/</span> Overview</div>
          <div className="topbar-right">
            <span className="preview-badge">DEVELOPER PREVIEW</span>
            <div className="user-avatar" aria-label="Local development user">T</div>
          </div>
        </header>

        <section className="page-content">
          <div className="intro-row">
            <div>
              <p className="eyebrow">AIRFREIGHT FORWARDING</p>
              <h1>Build the operation, one shipment at a time.</h1>
              <p className="intro-copy">
                The team workspace is set up. Start with governed master data, then connect it to shipment and AWB workflows.
              </p>
            </div>
            <div className="flight-stamp" aria-hidden="true">
              <span>OPS</span>
              <strong>01</strong>
              <span>FOUNDATION</span>
            </div>
          </div>

          <section className="status-panel" aria-labelledby="status-title">
            <div className="status-icon" aria-hidden="true">✓</div>
            <div className="status-copy">
              <h2 id="status-title">Your local foundation is in place</h2>
              <p>Repository, API shell, PostgreSQL, and web client are ready for the first vertical slice.</p>
            </div>
            <div className="status-tag"><span /> Ready to build</div>
          </section>

          <div className="section-heading">
            <div>
              <p className="eyebrow">DELIVERY SEQUENCE</p>
              <h2>Start here</h2>
            </div>
            <span className="sequence-note">FOUNDATION → OPERATIONS → INTEGRATION</span>
          </div>

          <div className="module-grid">
            {modules.map((module) => (
              <article className="module-card" key={module.number}>
                <div className="module-card-top">
                  <span className="module-number">{module.number}</span>
                  <span className={module.status === 'First build slice' ? 'module-status active-status' : 'module-status'}>
                    {module.status}
                  </span>
                </div>
                <h3>{module.name}</h3>
                <p>{module.description}</p>
                <div className="card-rule" />
                <span className="card-footer">AIRFREIGHT OPERATIONS</span>
              </article>
            ))}
          </div>

          <footer className="page-footer">
            <span>Architecture and build decisions are documented in the repository.</span>
            <span className="footer-code">TMS / LOCAL / 001</span>
          </footer>
        </section>
      </main>
    </div>
  )
}

export default App
