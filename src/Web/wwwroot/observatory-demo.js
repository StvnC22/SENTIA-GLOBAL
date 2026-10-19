(function () {
  let snapshot = null;
  let loadPromise = null;

  const originalRenderActiveView = renderActiveView;
  renderActiveView = function () {
    originalRenderActiveView();
    if (state.activeNav === "observatory") {
      ensureObservatoryLoaded();
    }
  };

  const originalUpdateNotch = updateNotch;
  updateNotch = function (statusText, isLoading) {
    originalUpdateNotch(statusText, isLoading);
    if (state.activeNav !== "observatory") return;
    const notchTitle = document.getElementById("notchTitle");
    const notchStatus = document.getElementById("notchStatus");
    const notchBadge = document.getElementById("notchBadge");
    if (notchTitle) notchTitle.textContent = "Observatorio Territorial";
    if (notchStatus && !getRunningJobs().length) notchStatus.textContent = "Demo aislada";
    if (notchBadge && !getRunningJobs().length) notchBadge.textContent = "DEMO";
  };

  function ensureObservatoryLoaded() {
    if (snapshot) {
      renderObservatory();
      return;
    }
    if (!loadPromise) {
      loadPromise = fetch("/api/observatory-demo")
        .then(response => {
          if (!response.ok) throw new Error("No se pudo cargar la demo");
          return response.json();
        })
        .then(data => {
          snapshot = data;
          fillFilterOptions();
          renderObservatory();
        })
        .catch(error => {
          loadPromise = null;
          const body = document.getElementById("obsTerritoryBody");
          if (body) {
            body.innerHTML = `<tr><td colspan="6">${escapeHtml(error.message)}</td></tr>`;
          }
        });
    }
  }

  function fillFilterOptions() {
    const territories = snapshot.territories || [];
    fillSelect("obsProvinceFilter", unique(territories.map(item => item.province)));
    fillSelect("obsCantonFilter", unique(territories.map(item => item.canton)));
    fillSelect("obsParishFilter", unique(territories.map(item => item.parish)));
  }

  function fillSelect(id, values) {
    const select = document.getElementById(id);
    if (!select) return;
    const current = select.value;
    select.innerHTML = `<option value="all">Todos</option>${values
      .map(value => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`)
      .join("")}`;
    if ([...select.options].some(option => option.value === current)) {
      select.value = current;
    }
  }

  function unique(values) {
    return [...new Set(values.filter(Boolean))];
  }

  function selectedTerritories() {
    const province = document.getElementById("obsProvinceFilter")?.value || "all";
    const canton = document.getElementById("obsCantonFilter")?.value || "all";
    const parish = document.getElementById("obsParishFilter")?.value || "all";
    return (snapshot.territories || []).filter(item =>
      (province === "all" || item.province === province)
      && (canton === "all" || item.canton === canton)
      && (parish === "all" || item.parish === parish)
    );
  }

  function renderObservatory() {
    if (!snapshot) return;
    const disclaimer = document.getElementById("observatoryDisclaimer");
    if (disclaimer) disclaimer.textContent = snapshot.disclaimer;

    const kpis = snapshot.kpis || {};
    const kpiGrid = document.getElementById("obsKpiGrid");
    if (kpiGrid) {
      kpiGrid.innerHTML = [
        ["Menciones demo", kpis.mentions],
        ["Positivo", `${Number(kpis.positivePercent || 0).toFixed(1)}%`],
        ["Negativo", `${Number(kpis.negativePercent || 0).toFixed(1)}%`],
        ["Alertas abiertas", kpis.openAlerts]
      ].map(([label, value]) => `
        <article class="glass-card obs-kpi">
          <span>${escapeHtml(String(label))}</span>
          <strong>${escapeHtml(String(value ?? "—"))}</strong>
        </article>
      `).join("");
    }

    const rows = selectedTerritories().map(item => `
      <tr>
        <td>${escapeHtml(item.province)}</td>
        <td>${escapeHtml(item.canton)}</td>
        <td>${escapeHtml(item.parish)}</td>
        <td>${item.mentions}</td>
        <td>${Number(item.negativityPercent).toFixed(1)}%</td>
        <td>${escapeHtml(item.dominantSentiment)}</td>
      </tr>
    `).join("");
    const body = document.getElementById("obsTerritoryBody");
    if (body) body.innerHTML = rows || `<tr><td colspan="6">Sin coincidencias para el filtro.</td></tr>`;

    const maxVolume = Math.max(...(snapshot.trend || []).map(point => point.volume), 1);
    const trend = document.getElementById("obsTrend");
    if (trend) {
      trend.innerHTML = (snapshot.trend || []).map(point => `
        <div class="obs-bar" title="${point.volume} menciones">
          <div class="obs-bar-fill" style="height:${Math.max(12, (point.volume / maxVolume) * 120)}px"></div>
          <span>${escapeHtml(point.label)}</span>
        </div>
      `).join("");
    }

    const alerts = document.getElementById("obsAlerts");
    if (alerts) {
      alerts.innerHTML = (snapshot.alerts || []).map(alert => `
        <article class="obs-item obs-sev-${escapeHtml(alert.severity)}">
          <strong>${escapeHtml(alert.territory)} · ${escapeHtml(alert.severity)}</strong>
          <small>${escapeHtml(alert.message)}</small>
          <small>Volumen ${alert.volume} · negatividad ${Number(alert.negativityPercent).toFixed(1)}% · ${escapeHtml(alert.channel)}</small>
        </article>
      `).join("");
    }

    const searches = document.getElementById("obsSearches");
    if (searches) {
      searches.innerHTML = (snapshot.searches || []).map(search => `
        <article class="obs-item">
          <strong>${escapeHtml(search.query)}</strong>
          <small>${escapeHtml(search.territory)} · ${(search.networks || []).join(", ")} · ${escapeHtml(search.cadence)}</small>
          <small>Última: ${formatStamp(search.lastRunUtc)} · Próxima: ${formatStamp(search.nextRunUtc)} · ${escapeHtml(search.status)}</small>
        </article>
      `).join("");
    }

    const report = snapshot.report || {};
    const reportEl = document.getElementById("obsReport");
    if (reportEl) {
      reportEl.innerHTML = `
        <h4>${escapeHtml(report.title || "")}</h4>
        <p>${escapeHtml(report.findings || "")}</p>
        <p>Sentimiento promedio (escala -1 a 1): <strong>${Number(report.averageSentiment || 0).toFixed(2)}</strong></p>
        <div class="obs-topics">${(report.topics || []).map(topic => `<em>${escapeHtml(topic)}</em>`).join("")}</div>
      `;
    }

    const roles = document.getElementById("obsRoles");
    if (roles) {
      roles.innerHTML = (snapshot.roles || []).map(role => `
        <article class="obs-item">
          <strong>${escapeHtml(role.role)}</strong>
          <small>${escapeHtml(role.summary)}</small>
          <small>Tokens: ${flag(role.configureTokens)} · Búsquedas: ${flag(role.designSearches)} · Exportar: ${flag(role.exportReports)} · Usuarios: ${flag(role.manageUsers)}</small>
        </article>
      `).join("");
    }

    const quality = snapshot.quality || {};
    const qualityEl = document.getElementById("obsQuality");
    if (qualityEl) {
      qualityEl.innerHTML = `
        <p>Duplicados retirados: <strong>${quality.duplicatesRemoved}</strong></p>
        <p>Spam: <strong>${quality.spamFlagged}</strong> · Bots: <strong>${quality.botsFlagged}</strong></p>
        <p>Fuentes no oficiales: <strong>${quality.unofficialSources}</strong></p>
        <p>Confiabilidad estimada: <strong>${Number(quality.reliablePercent || 0).toFixed(1)}%</strong></p>
      `;
    }

    const audit = document.getElementById("obsAudit");
    if (audit) {
      audit.innerHTML = (snapshot.audit || []).map(event => `
        <article class="obs-item">
          <strong>${escapeHtml(event.actor)}</strong>
          <small>${escapeHtml(event.action)} · ${escapeHtml(event.resource)}</small>
          <small>${formatStamp(event.atUtc)}</small>
        </article>
      `).join("");
    }

    const toolsBody = document.getElementById("obsToolsBody");
    if (toolsBody) {
      toolsBody.innerHTML = (snapshot.tools || []).map(tool => `
        <tr>
          <td>${escapeHtml(tool.need)}</td>
          <td>${escapeHtml(tool.inThisDemo)}</td>
          <td>${escapeHtml(tool.productionTool)}</td>
        </tr>
      `).join("");
    }
  }

  function flag(value) {
    return value ? "sí" : "no";
  }

  function formatStamp(value) {
    if (!value) return "—";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "—";
    return date.toLocaleString("es-EC");
  }

  document.addEventListener("change", event => {
    if (!event.target || !["obsProvinceFilter", "obsCantonFilter", "obsParishFilter"].includes(event.target.id)) {
      return;
    }
    if (snapshot) renderObservatory();
  });

  document.addEventListener("click", event => {
    const exportBtn = event.target.closest("#observatoryExportCsvBtn");
    if (exportBtn) {
      event.preventDefault();
      window.location.href = "/api/observatory-demo/export?format=csv";
      return;
    }
    const printBtn = event.target.closest("#observatoryPrintBtn");
    if (printBtn) {
      event.preventDefault();
      window.print();
    }
  });
})();
