/* ==========================================================================
   SENTIA GLOBAL - APPLICATION LOGIC & STATE (NET 10 / ASP.NET CORE)
   ========================================================================== */

const state = {
  activeNav: "dashboard",
  comments: [],
  summary: null,
  platform: "global",
  prompt: "",
  query: "",
  sentiment: "all",
  sort: "newest",
  periodDays: 400,
  sources: { instagram: "", facebook: "", tiktok: "", x: "" },
  prompts: { instagram: "", facebook: "", tiktok: "", x: "", global: "" },
  credentialStatus: null,
  credentialMasks: {},
  jobs: {},
  history: [],
  theme: "dark"
};

const credentialFields = {
  instagram: "instagramApiKey",
  facebook: "facebookApiKey",
  tikTok: "tiktokApiKey",
  x: "xApiKey",
  artificialIntelligence: "aiApiKey"
};

const settingsPageCredentialFields = {
  instagram: "settingsInstagramApiKey",
  facebook: "settingsFacebookApiKey",
  tikTok: "settingsTiktokApiKey",
  x: "settingsXApiKey",
  artificialIntelligence: "settingsAiApiKey"
};

const configuredMask = "••••••••••••••••";

const escapeHtml = value => String(value ?? "").replace(/[&<>'"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character]);
const initials = name => (name || "").split(/\s+/).slice(0, 2).map(part => part[0]).join("").toUpperCase() || "?";
const percent = (value, total) => total ? Math.round(value / total * 100) : 0;
const formatDay = date => new Intl.DateTimeFormat("es-EC", { day: "2-digit", month: "short" }).format(date).replace(".", "");
const formatTime = date => new Intl.DateTimeFormat("es-EC", { hour: "2-digit", minute: "2-digit" }).format(date);
const platformLabel = platform => ({ instagram: "Instagram", facebook: "Facebook", tiktok: "TikTok", x: "X", global: "Todas las redes" }[platform] || platform);

// Keep the platform identity consistent even if an actor/provider returns the
// legacy names "twitter" or "x.com". The UI uses "x" everywhere internally.
function normalizePlatform(platform) {
  const value = String(platform ?? "").trim().toLocaleLowerCase("en");
  if (value === "all") return "global";
  if (value === "twitter" || value === "twitter.com" || value === "x.com" || value === "x-twitter") return "x";
  return value;
}

/* ========================================== */
/* THEME MANAGER                              */
/* ========================================== */
function initTheme() {
  const savedTheme = localStorage.getItem("sentia-theme") || "dark";
  setTheme(savedTheme);
}

function setTheme(theme) {
  state.theme = theme;
  document.documentElement.setAttribute("data-theme", theme);
  localStorage.setItem("sentia-theme", theme);
  
  document.querySelectorAll(".theme-btn").forEach(btn => {
    btn.classList.toggle("active", btn.dataset.themeVal === theme);
  });
}

/* ========================================== */
/* NAVIGATION & VIEW ROUTER                   */
/* ========================================== */
function initNavigation() {
  document.querySelectorAll("[data-nav]").forEach(element => {
    element.addEventListener("click", event => {
      event.preventDefault();
      const targetNav = element.dataset.nav;
      navigateTo(targetNav);
    });
  });

  // Mobile menu handlers
  const mobileMenuBtn = document.getElementById("mobileMenuBtn");
  const sidebarCloseBtn = document.getElementById("sidebarCloseBtn");
  const sidebarBackdrop = document.getElementById("sidebarBackdrop");
  const appSidebar = document.getElementById("appSidebar");

  function openMobileSidebar() {
    appSidebar.classList.add("mobile-open");
    sidebarBackdrop.classList.add("mobile-open");
  }

  function closeMobileSidebar() {
    appSidebar.classList.remove("mobile-open");
    sidebarBackdrop.classList.remove("mobile-open");
  }

  if (mobileMenuBtn) mobileMenuBtn.addEventListener("click", openMobileSidebar);
  if (sidebarCloseBtn) sidebarCloseBtn.addEventListener("click", closeMobileSidebar);
  if (sidebarBackdrop) sidebarBackdrop.addEventListener("click", closeMobileSidebar);
}

function navigateTo(navId) {
  state.activeNav = navId;

  // Update sidebar active item
  document.querySelectorAll(".sidebar-nav .nav-item").forEach(item => {
    item.classList.toggle("active", item.dataset.nav === navId);
  });

  // Update view panel visibility
  document.querySelectorAll(".view-panel").forEach(panel => {
    panel.classList.toggle("active", panel.id === `view-${navId}`);
  });

  // Close mobile sidebar
  document.getElementById("appSidebar").classList.remove("mobile-open");
  document.getElementById("sidebarBackdrop").classList.remove("mobile-open");

  // Scroll to top
  window.scrollTo({ top: 0, behavior: "smooth" });

  // Update dynamic notch and view components
  updateNotch();
  renderActiveView();
}

function updateNotch(statusText = null, isLoading = false) {
  const notchTitle = document.getElementById("notchTitle");
  const notchStatus = document.getElementById("notchStatus");
  const notchBadge = document.getElementById("notchBadge");
  const notchPulseDot = document.getElementById("notchPulseDot");

  const titleMap = {
    dashboard: "Dashboard Principal",
    instagram: "Módulo Instagram",
    facebook: "Módulo Facebook",
    tiktok: "Módulo TikTok",
    x: "Módulo X (Twitter)",
    global: "Todas las redes",
    history: "Historial de Análisis",
    settings: "Configuración y API Keys"
  };

  if (notchTitle) notchTitle.textContent = titleMap[state.activeNav] || "Sentia Global";

  const runningJobs = getRunningJobs();
  const hasRunningJobs = runningJobs.length > 0;

  if (notchPulseDot) {
    notchPulseDot.classList.toggle("loading", hasRunningJobs || isLoading);
  }

  if (notchStatus) {
    if (hasRunningJobs) {
      if (runningJobs.length === 1) {
        notchStatus.textContent = runningJobs[0].progressText;
      } else {
        notchStatus.textContent = `${runningJobs.length} análisis en ejecución`;
      }
    } else {
      notchStatus.textContent = statusText || (state.dataSource === "live" ? "Datos Reales" : "Sistema Activo");
    }
  }

  const commentsCount = commentsInPeriod().length;
  if (notchBadge) {
    if (hasRunningJobs) {
      notchBadge.textContent = "Ejecutando...";
    } else {
      notchBadge.textContent = `${commentsCount} coms`;
    }
  }
}

/* ========================================== */
/* COMMENTS & METRICS DATA FILTERING          */
/* ========================================== */
function commentsInPeriod() {
  if (!state.comments.length) return [];
  if (!state.periodDays || state.periodDays <= 0) return [...state.comments];
  const cutoff = Date.now() - state.periodDays * 24 * 60 * 60 * 1000;
  return state.comments.filter(comment => {
    const time = new Date(comment.publishedAt).getTime();
    if (Number.isNaN(time)) return true;
    return time >= cutoff;
  });
}

function filterComments(platformFilter = "global", sentimentFilter = "all", searchQuery = "", sortFilter = "newest") {
  const searchTerm = searchQuery.trim().toLocaleLowerCase("es");
  const normalizedFilter = normalizePlatform(platformFilter);
  return commentsInPeriod()
    .filter(item => normalizedFilter === "global" || normalizePlatform(item.platform) === normalizedFilter)
    .filter(item => sentimentFilter === "all" || item.sentiment.label === sentimentFilter)
    .filter(item => !searchTerm || `${item.authorName} ${item.authorHandle} ${item.text} ${item.postTitle}`.toLocaleLowerCase("es").includes(searchTerm))
    .sort((left, right) => {
      if (sortFilter === "oldest") return new Date(left.publishedAt) - new Date(right.publishedAt);
      if (sortFilter === "post") return left.postTitle.localeCompare(right.postTitle, "es");
      if (sortFilter === "sentiment") return left.sentiment.displayName.localeCompare(right.sentiment.displayName, "es");
      return new Date(right.publishedAt) - new Date(left.publishedAt);
    });
}

/* ========================================== */
/* VIEW RENDERING FUNCTIONS                   */
/* ========================================== */
function renderActiveView() {
  renderMetrics();
  renderSidebarCounts();
  renderJobBanners();
  
  if (state.activeNav === "dashboard") {
    renderDashboard();
  } else if (["instagram", "facebook", "tiktok", "x"].includes(state.activeNav)) {
    renderNetworkModule(state.activeNav);
  } else if (state.activeNav === "global") {
    renderGlobalView();
  } else if (state.activeNav === "history") {
    renderHistoryTable();
  }
}

function renderSidebarCounts() {
  const periodComments = commentsInPeriod();
  for (const platform of ["instagram", "facebook", "tiktok", "x"]) {
    const count = periodComments.filter(c => normalizePlatform(c.platform) === platform).length;
    document.querySelectorAll(`[data-sidebar-count="${platform}"]`).forEach(el => el.textContent = count);
  }
  document.querySelectorAll('[data-sidebar-count="global"]').forEach(el => el.textContent = periodComments.length);
  
  const historyBadge = document.getElementById("historyBadge");
  if (historyBadge) historyBadge.textContent = state.history.length;
}

function renderMetrics() {
  const periodComments = commentsInPeriod();
  const summary = {
    total: periodComments.length,
    positive: periodComments.filter(comment => comment.sentiment.label === "positive").length,
    neutral: periodComments.filter(comment => comment.sentiment.label === "neutral").length,
    negative: periodComments.filter(comment => comment.sentiment.label === "negative").length
  };

  // Dashboard Metrics
  const setElemText = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
  setElemText("dashTotalMetric", summary.total);
  setElemText("totalMetric", summary.total);
  
  for (const label of ["positive", "neutral", "negative"]) {
    const value = summary[label];
    const ratio = percent(value, summary.total);
    
    setElemText(`dash${label.charAt(0).toUpperCase() + label.slice(1)}Metric`, value);
    setElemText(`dash${label.charAt(0).toUpperCase() + label.slice(1)}Percent`, `${ratio}% del total`);
    
    const bar = document.getElementById(`dash${label.charAt(0).toUpperCase() + label.slice(1)}Bar`);
    if (bar) bar.style.width = `${ratio}%`;

    setElemText(`${label}Metric`, value);
    setElemText(`${label}Percent`, `${ratio}% del total`);
  }
}

function renderDashboard() {
  const periodComments = commentsInPeriod();

  // Render Network Distribution Bars
  const counts = { instagram: 0, facebook: 0, tiktok: 0, x: 0 };
  periodComments.forEach(c => {
    const platform = normalizePlatform(c.platform);
    if (counts[platform] !== undefined) counts[platform]++;
  });

  const maxCount = Math.max(...Object.values(counts), 1);
  for (const [net, count] of Object.entries(counts)) {
    const countEl = document.getElementById(`dashCount${net.charAt(0).toUpperCase() + net.slice(1)}`);
    const barEl = document.getElementById(`dashBar${net.charAt(0).toUpperCase() + net.slice(1)}`);
    if (countEl) countEl.textContent = count;
    if (barEl) barEl.style.width = `${Math.round((count / maxCount) * 100)}%`;
  }

  // Top Network
  const topNetEntry = Object.entries(counts).sort((a, b) => b[1] - a[1])[0];
  const topNetEl = document.getElementById("dashTopNetwork");
  if (topNetEl) {
    topNetEl.textContent = topNetEntry && topNetEntry[1] > 0
      ? `${platformLabel(topNetEntry[0])} (${topNetEntry[1]})`
      : "Sin comentarios";
  }

  // Last Analysis Timestamp
  const lastAnalysisEl = document.getElementById("dashLastAnalysis");
  if (lastAnalysisEl) {
    if (state.history.length > 0) {
      lastAnalysisEl.textContent = state.history[0].formattedDate;
    } else {
      lastAnalysisEl.textContent = "Sin ejecuciones";
    }
  }

  // Render Clouds & Calendars
  renderSemanticCloud("dashSemanticCloud", "dashCloudMeta");
}

function renderNetworkModule(platform) {
  const searchInput = document.querySelector(`.table-search-input[data-platform="${platform}"]`);
  const sentimentSelect = document.querySelector(`.sentiment-filter-select[data-platform="${platform}"]`);
  const sortSelect = document.querySelector(`.sort-filter-select[data-platform="${platform}"]`);

  const searchQuery = searchInput ? searchInput.value : "";
  const sentimentFilter = sentimentSelect ? sentimentSelect.value : "all";
  const sortFilter = sortSelect ? sortSelect.value : "newest";

  const comments = filterComments(platform, sentimentFilter, searchQuery, sortFilter);
  const allPlatformComments = commentsInPeriod().filter(c => normalizePlatform(c.platform) === platform);

  // Network Metrics
  const setElemText = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
  setElemText(`${platform}MetricTotal`, allPlatformComments.length);
  setElemText(`${platform}MetricPositive`, allPlatformComments.filter(c => c.sentiment.label === "positive").length);
  setElemText(`${platform}MetricNeutral`, allPlatformComments.filter(c => c.sentiment.label === "neutral").length);
  setElemText(`${platform}MetricNegative`, allPlatformComments.filter(c => c.sentiment.label === "negative").length);

  // Table Body
  const bodyEl = document.getElementById(`${platform}CommentsBody`);
  const emptyEl = document.getElementById(`${platform}EmptyState`);

  if (bodyEl) {
    bodyEl.innerHTML = comments.map(comment => renderCommentRow(comment, false)).join("");
  }
  if (emptyEl) {
    emptyEl.hidden = comments.length > 0;
    // A non-empty metric with an empty table means a search/sentiment filter
    // is active, not that the API returned no comments.
    if (comments.length === 0 && allPlatformComments.length > 0) {
      emptyEl.querySelector("strong")?.replaceChildren(document.createTextNode("No hay coincidencias con los filtros"));
      emptyEl.querySelector("p")?.replaceChildren(document.createTextNode("Borra la búsqueda o selecciona Todas las emociones para ver los comentarios."));
    }
  }

  renderSemanticCloud(`${platform}SemanticCloud`, `${platform}CloudMeta`, platform);
}

function renderGlobalView() {
  const searchInput = document.getElementById("searchInput");
  const platformSelect = document.getElementById("globalPlatformSelect");

  const searchQuery = searchInput ? searchInput.value : "";
  const platformFilter = platformSelect ? platformSelect.value : "all";

  const comments = filterComments(platformFilter, state.sentiment, searchQuery, state.sort);

  const bodyEl = document.getElementById("commentsBody");
  const emptyEl = document.getElementById("emptyState");
  const footerCount = document.getElementById("footerCount");

  if (bodyEl) {
    bodyEl.innerHTML = comments.map(comment => renderCommentRow(comment, true)).join("");
  }
  if (emptyEl) {
    emptyEl.hidden = comments.length > 0;
  }
  if (footerCount) {
    footerCount.textContent = `Mostrando ${comments.length} ${comments.length === 1 ? "comentario" : "comentarios"}`;
  }

  renderSemanticCloud("semanticCloud", "cloudMeta", platformFilter === "all" ? "global" : platformFilter);
}

function renderCommentRow(comment, showNetworkBadge = true) {
  const date = new Date(comment.publishedAt);
  const netBadge = showNetworkBadge ? `<td data-label="Red"><span class="history-net-pill ${escapeHtml(comment.platform)}">${escapeHtml(comment.platform)}</span></td>` : "";
  return `<tr>
    ${netBadge}
    <td data-label="Usuario"><a class="user-link" href="${escapeHtml(comment.authorUrl)}" target="_blank" rel="noopener noreferrer"><span class="user-avatar">${escapeHtml(initials(comment.authorName))}</span><span class="user-info"><strong>${escapeHtml(comment.authorName)}</strong><span>${escapeHtml(comment.authorHandle)}</span></span></a></td>
    <td data-label="Comentario"><a class="comment-link" href="${escapeHtml(comment.commentUrl)}" target="_blank" rel="noopener noreferrer">${escapeHtml(comment.text)}</a></td>
    <td data-label="Publicación"><a class="post-link" href="${escapeHtml(comment.postUrl)}" target="_blank" rel="noopener noreferrer"><span>${escapeHtml(comment.postTitle)}</span></a></td>
    <td data-label="Sentimiento"><span class="sentiment ${escapeHtml(comment.sentiment.label)}"><i></i>${escapeHtml(comment.sentiment.displayName)}</span><span class="confidence">${comment.sentiment.confidence}% confianza</span></td>
    <td class="date" data-label="Fecha"><strong>${escapeHtml(formatDay(date))}</strong><span>${escapeHtml(formatTime(date))}</span></td>
  </tr>`;
}

/* ========================================== */
/* SEMANTIC CLOUD & CALENDAR                  */
/* ========================================== */
const semanticStopWords = new Set("a al algo alguna algunas alguno algunos ante antes como con contra cual cuando de del desde donde dos el ella ellas ellos en entre era es esa esas ese eso esos esta estas este esto estos fue ha hay la las le les lo los más me mi mis mucho muy no nos o para pero por que se sin sobre su sus también te un una uno unos y ya yo".split(" "));

function renderSemanticCloud(containerId, metaId, platformFilter = "global") {
  const container = document.getElementById(containerId);
  const meta = document.getElementById(metaId);
  if (!container) return;

  const comments = commentsInPeriod().filter(c => platformFilter === "global" || platformFilter === "all" || c.platform === platformFilter);
  const counts = new Map();

  for (const comment of comments) {
    const text = `${comment.text} ${comment.postTitle}`.toLocaleLowerCase("es");
    for (const word of text.normalize("NFD").replace(/[\u0300-\u036f]/g, "").match(/[a-záéíóúñü]{4,}/gi) || []) {
      const normalized = word.toLocaleLowerCase("es");
      if (semanticStopWords.has(normalized) || /^https?$/.test(normalized)) continue;
      counts.set(normalized, (counts.get(normalized) || 0) + 1);
    }
  }

  const words = [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 28);
  if (!words.length) {
    container.innerHTML = '<span class="insight-empty">No hay suficientes palabras para construir la nube.</span>';
    if (meta) meta.textContent = "0 términos";
    return;
  }

  const max = words[0][1];
  container.innerHTML = words.map(([word, count], index) => {
    const size = 0.82 + (count / max) * 1.05;
    return `<span class="cloud-word cloud-tone-${index % 5}" style="font-size:${size.toFixed(2)}rem" title="${count} apariciones">${escapeHtml(word)}<sup>${count}</sup></span>`;
  }).join("");

  if (meta) meta.textContent = `${words.length} términos`;
}

function renderCalendar(containerId, metaId) {
  const container = document.getElementById(containerId);
  const meta = document.getElementById(metaId);
  if (!container) return;

  const comments = commentsInPeriod();
  if (!comments.length) {
    container.innerHTML = '<span class="insight-empty">No hay actividad en el periodo.</span>';
    if (meta) meta.textContent = "0 días";
    return;
  }

  const byDay = new Map();
  comments.forEach(comment => {
    const key = new Date(comment.publishedAt).toISOString().slice(0, 10);
    byDay.set(key, (byDay.get(key) || 0) + 1);
  });

  const end = new Date();
  end.setHours(0, 0, 0, 0);
  const days = Math.min(state.periodDays || 30, 42);
  const start = new Date(end);
  start.setDate(end.getDate() - days + 1);

  const peak = Math.max(...byDay.values(), 1);
  const cells = [];
  for (let i = 0; i < days; i += 1) {
    const date = new Date(start);
    date.setDate(start.getDate() + i);
    const key = date.toISOString().slice(0, 10);
    const count = byDay.get(key) || 0;
    const level = count === 0 ? 0 : Math.min(3, Math.ceil(count / peak * 3));
    cells.push(`<span class="calendar-cell level-${level}" title="${escapeHtml(date.toLocaleDateString("es-EC"))}: ${count} comentario${count === 1 ? "" : "s"}"></span>`);
  }

  container.innerHTML = cells.join("");
  if (meta) meta.textContent = `${byDay.size} días activos`;
}

/* ========================================== */
/* HISTORIAL SYSTEM (LOCALSTORAGE)            */
/* ========================================== */
function loadHistory() {
  try {
    const raw = localStorage.getItem("sentia-history-v1");
    if (raw) state.history = JSON.parse(raw);
  } catch (e) {
    state.history = [];
  }
}

function saveHistoryRecord(dashboard, assignedPlatforms, promptText) {
  if (!dashboard) return;
  const record = {
    id: "hist_" + Date.now(),
    timestamp: new Date().toISOString(),
    formattedDate: new Intl.DateTimeFormat("es-EC", { dateStyle: "short", timeStyle: "medium" }).format(new Date()),
    platforms: assignedPlatforms.map(item => item.platform),
    urls: { ...state.sources },
    prompt: promptText || "",
    totalComments: dashboard.comments ? dashboard.comments.length : 0,
    positive: (dashboard.comments || []).filter(c => c.sentiment?.label === "positive").length,
    neutral: (dashboard.comments || []).filter(c => c.sentiment?.label === "neutral").length,
    negative: (dashboard.comments || []).filter(c => c.sentiment?.label === "negative").length,
    dataSource: dashboard.dataSource || "demo",
    message: dashboard.message || ""
  };

  state.history.unshift(record);
  if (state.history.length > 50) state.history = state.history.slice(0, 50);

  try {
    localStorage.setItem("sentia-history-v1", JSON.stringify(state.history));
  } catch (e) {}

  renderSidebarCounts();
}

function renderHistoryTable() {
  const bodyEl = document.getElementById("historyTableBody");
  const emptyEl = document.getElementById("historyEmptyState");
  if (!bodyEl) return;

  if (!state.history.length) {
    bodyEl.innerHTML = "";
    if (emptyEl) emptyEl.hidden = false;
    return;
  }

  if (emptyEl) emptyEl.hidden = true;

  bodyEl.innerHTML = state.history.map(item => {
    const netBadges = item.platforms.map(p => `<span class="history-net-pill ${escapeHtml(p)}">${escapeHtml(p)}</span>`).join("");
    const statusClass = item.dataSource === "live" ? "live" : "demo";
    const statusText = item.dataSource === "live" ? "Real (API)" : "Demostrativo";

    return `<tr>
      <td><strong>${escapeHtml(item.formattedDate)}</strong></td>
      <td><div class="history-net-badges">${netBadges}</div></td>
      <td><strong>${item.totalComments}</strong> coms</td>
      <td><span class="sentiment positive">${item.positive}</span> / <span class="sentiment neutral">${item.neutral}</span> / <span class="sentiment negative">${item.negative}</span></td>
      <td><span class="status-tag ${statusClass}">${statusText}</span></td>
      <td>
        <div class="history-actions-group">
          <button class="action-icon-btn" onclick="openHistoryDetail('${item.id}')" title="Ver resumen">
            <svg viewBox="0 0 24 24"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
          </button>
          <button class="action-icon-btn" onclick="reloadHistoryQuery('${item.id}')" title="Reactivar consulta">
            <svg viewBox="0 0 24 24"><path d="M23 4v6h-6M1 20v-6h6"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg>
          </button>
          <button class="action-icon-btn" onclick="exportHistoryRecord('${item.id}')" title="Exportar CSV">
            <svg viewBox="0 0 24 24"><path d="M12 3v12m0 0 4-4m-4 4-4-4M5 20h14"/></svg>
          </button>
          <button class="action-icon-btn danger" onclick="deleteHistoryRecord('${item.id}')" title="Eliminar registro">
            <svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
          </button>
        </div>
      </td>
    </tr>`;
  }).join("");
}

window.openHistoryDetail = function(id) {
  const record = state.history.find(r => r.id === id);
  if (!record) return;

  const modal = document.getElementById("historyDetailDialog");
  const content = document.getElementById("historyModalContent");
  const reloadBtn = document.getElementById("historyModalReloadBtn");

  if (content) {
    const urlsList = Object.entries(record.urls)
      .filter(([_, url]) => url)
      .map(([net, url]) => `<li><strong>${platformLabel(net)}:</strong> ${escapeHtml(url)}</li>`)
      .join("");

    content.innerHTML = `
      <div style="display:flex; flex-direction:column; gap:1rem; font-size:0.9rem;">
        <div><strong>Fecha:</strong> ${escapeHtml(record.formattedDate)}</div>
        <div><strong>Estado:</strong> ${record.dataSource === "live" ? "Conexión Real" : "Demostrativo"}</div>
        <div><strong>Prompt Utilizado:</strong> ${escapeHtml(record.prompt || "Ninguno (Recomendado)")}</div>
        <div>
          <strong>Desglose de Comentarios:</strong>
          <ul style="margin-top:0.35rem; padding-left:1.2rem;">
            <li>Total: ${record.totalComments}</li>
            <li>Positivos: ${record.positive}</li>
            <li>Neutrales: ${record.neutral}</li>
            <li>Negativos: ${record.negative}</li>
          </ul>
        </div>
        <div>
          <strong>URLs Consultadas:</strong>
          <ul style="margin-top:0.35rem; padding-left:1.2rem;">${urlsList || "<li>Ninguna URL registrada</li>"}</ul>
        </div>
        ${record.message ? `<div style="color:var(--sentiment-negative);"><strong>Mensaje:</strong> ${escapeHtml(record.message)}</div>` : ""}
      </div>
    `;
  }

  if (reloadBtn) {
    reloadBtn.onclick = () => {
      modal.close();
      reloadHistoryQuery(id);
    };
  }

  modal.showModal();
};

window.reloadHistoryQuery = function(id) {
  const record = state.history.find(r => r.id === id);
  if (!record) return;

  for (const [net, url] of Object.entries(record.urls)) {
    state.sources[net] = url || "";
    syncSourceInput(net, url || "");
  }

  if (record.prompt) {
    state.prompt = record.prompt;
    syncPromptInputs(record.prompt);
  }

  showToast("Consulta del historial cargada en los campos");
  navigateTo(record.platforms.length === 1 ? record.platforms[0] : "global");
  loadComments({ notify: true, force: true });
};

window.exportHistoryRecord = function(id) {
  const record = state.history.find(r => r.id === id);
  if (!record) return;

  const rows = [
    ["Campo", "Valor"],
    ["ID", record.id],
    ["Fecha", record.formattedDate],
    ["Redes", record.platforms.join(", ")],
    ["Total Comentarios", record.totalComments],
    ["Positivos", record.positive],
    ["Neutrales", record.neutral],
    ["Negativos", record.negative],
    ["Prompt", record.prompt],
    ["Estado", record.dataSource],
    ["URLs", JSON.stringify(record.urls)]
  ];

  const csv = rows.map(r => r.map(v => `"${String(v).replaceAll('"', '""')}"`).join(",")).join("\n");
  downloadCsv(csv, `historial-sentia-${record.id}.csv`);
  showToast("Registro de historial exportado");
};

window.deleteHistoryRecord = function(id) {
  state.history = state.history.filter(r => r.id !== id);
  try {
    localStorage.setItem("sentia-history-v1", JSON.stringify(state.history));
  } catch (e) {}
  renderHistoryTable();
  renderSidebarCounts();
  showToast("Registro eliminado");
};

function clearAllHistory() {
  if (confirm("¿Estás seguro de que deseas eliminar todo el historial?")) {
    state.history = [];
    localStorage.removeItem("sentia-history-v1");
    renderHistoryTable();
    renderSidebarCounts();
    showToast("Historial limpiado completamente");
  }
}

function exportFullHistoryLog() {
  if (!state.history.length) {
    showToast("No hay registros en el historial");
    return;
  }

  const rows = [
    ["Fecha", "Redes", "Total", "Positivos", "Neutrales", "Negativos", "Estado", "Prompt"],
    ...state.history.map(item => [
      item.formattedDate,
      item.platforms.join(" | "),
      item.totalComments,
      item.positive,
      item.neutral,
      item.negative,
      item.dataSource,
      item.prompt
    ])
  ];

  const csv = rows.map(r => r.map(v => `"${String(v).replaceAll('"', '""')}"`).join(",")).join("\n");
  downloadCsv(csv, `historial-completo-sentia-${Date.now()}.csv`);
  showToast("Historial completo exportado");
}

/* ========================================== */
/* CREDENTIALS & API KEYS MANAGEMENT         */
/* ========================================== */
function applyCredentialInputs(status = state.credentialStatus) {
  for (const [name, inputId] of Object.entries(credentialFields)) {
    const input = document.getElementById(inputId);
    const settingsInput = document.getElementById(settingsPageCredentialFields[name]);
    const isConfigured = Boolean(status?.[name]);

    [input, settingsInput].forEach(el => {
      if (!el || el.dataset.editing === "true") return;
      if (isConfigured) {
        el.value = configuredMask;
        el.dataset.configured = "true";
        el.classList.add("configured");
      } else {
        el.value = "";
        delete el.dataset.configured;
        el.classList.remove("configured");
      }
    });
  }
}

function renderCredentialStatus(status) {
  state.credentialStatus = status;
  for (const name of ["instagram", "facebook", "tikTok", "x", "artificialIntelligence"]) {
    const isConfigured = Boolean(status[name]);
    document.querySelectorAll(`[data-key-status="${name}"]`).forEach(badge => {
      badge.textContent = isConfigured ? "Configurada" : "Sin configurar";
      badge.classList.toggle("configured", isConfigured);
    });
  }

  // Update Sidebar Health Badges
  const apifyOnline = status.instagram || status.facebook || status.tikTok || status.x;
  const apifyDot = document.getElementById("apifySidebarDot");
  const apifyText = document.getElementById("apifySidebarText");
  const apifyBadge = document.getElementById("apifyHealthBadge");

  if (apifyDot) apifyDot.className = `status-indicator-dot ${apifyOnline ? "online" : "warning"}`;
  if (apifyText) apifyText.textContent = apifyOnline ? "Conectado" : "Sin clave";
  if (apifyBadge) {
    apifyBadge.textContent = apifyOnline ? "Conectado" : "Sin configurar";
    apifyBadge.classList.toggle("configured", apifyOnline);
  }

  const geminiOnline = Boolean(status.artificialIntelligence);
  const geminiDot = document.getElementById("geminiSidebarDot");
  const geminiText = document.getElementById("geminiSidebarText");
  const geminiBadge = document.getElementById("geminiHealthBadge");

  if (geminiDot) geminiDot.className = `status-indicator-dot ${geminiOnline ? "online" : "warning"}`;
  if (geminiText) geminiText.textContent = geminiOnline ? "Conectado" : "Analizador Local";
  if (geminiBadge) {
    geminiBadge.textContent = geminiOnline ? "Gemini IA" : "Analizador Local";
    geminiBadge.classList.toggle("configured", geminiOnline);
  }

  applyCredentialInputs(status);
}

async function refreshCredentialStatus() {
  try {
    const response = await fetch("/api/api-credentials");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    renderCredentialStatus(await response.json());
  } catch (error) {
    console.error("Error cargando estado de credenciales:", error);
  }
}

async function saveApiKeysPayload(payload) {
  const response = await fetch("/api/api-credentials", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    let errorMsg = `HTTP ${response.status}`;
    try {
      const prob = await response.json();
      errorMsg = prob.detail || prob.title || errorMsg;
    } catch (e) {}
    throw new Error(errorMsg);
  }

  const status = await response.json();
  renderCredentialStatus(status);
  showToast("Claves de conexión guardadas exitosamente.");
}

function buildCredentialPayload(formData) {
  const payload = {};
  for (const [name, value] of formData.entries()) {
    const trimmed = String(value).trim();
    if (!trimmed || trimmed.includes("•")) continue;
    payload[name] = trimmed;
  }
  return payload;
}

/* ========================================== */
/* API CALLS & COMMENTS EXTRACTION           */
/* ========================================== */
function validateSourceUrl(platform, rawUrl) {
  const value = (rawUrl || "").trim();
  if (!value) return { status: "empty" };

  let uri;
  try { uri = new URL(value); } catch {
    return { status: "invalid", message: `URL de ${platformLabel(platform)} incorrecta: usa un enlace completo (https://…).` };
  }

  if (!/^https?:$/i.test(uri.protocol)) {
    return { status: "invalid", message: `URL de ${platformLabel(platform)} incorrecta: debe empezar con https://` };
  }

  const host = uri.hostname.toLowerCase();
  const path = uri.pathname.replace(/^\/+|\/+$/g, "");
  const rules = {
    instagram: () => (host.includes("instagram.com") || host === "instagr.am") && path.length > 0,
    facebook: () => {
      if (!(host.includes("facebook.com") || host.includes("fb.com") || host.includes("fb.watch"))) return false;
      if (path.toLowerCase() === "profile.php") return uri.searchParams.has("id");
      return path.length > 0 || uri.search.length > 1;
    },
    tiktok: () => host.includes("tiktok.com") && path.length > 0,
    x: () => (host.includes("x.com") || host.includes("twitter.com")) && path.length > 0
  };

  if (!(rules[platform]?.() ?? true)) {
    return { status: "invalid", message: `URL de ${platformLabel(platform)} incorrecta.` };
  }

  return { status: "ok", url: value };
}

function isDirectPostUrl(platform, url) {
  try {
    const uri = new URL(url);
    const path = uri.pathname.toLowerCase();
    if (platform === "tiktok") return /\/@[^/]+\/video\/\d+/.test(path);
    if (platform === "x") return /\/status\/\d+/.test(path);
    if (platform === "instagram") return /\/(p|reel|tv)\//.test(path);
    if (platform === "facebook") return /\/(posts|videos|watch|permalink|share)\//.test(path) || uri.searchParams.has("story_fbid");
  } catch (e) {
    return false;
  }
  return false;
}

async function fetchPlatformComments(item, { force, signal, prompt }) {
  const query = new URLSearchParams();
  query.set("platform", item.platform);
  query.set(`${item.platform}Url`, item.url);
  if (prompt) query.set("prompt", prompt);
  if (force || isDirectPostUrl(item.platform, item.url)) {
    query.set("refresh", "true");
  }

  let response = await fetch(`/api/social-comments?${query}`, { signal });
  if (!response.ok) {
    let probText = `HTTP ${response.status}`;
    try { const prob = await response.json(); probText = prob.detail || prob.title || probText; } catch (e) {}
    throw new Error(probText);
  }
  return await response.json();
}

/* ========================================== */
/* BACKGROUND ANALYSIS JOB MANAGER SYSTEM     */
/* ========================================== */

function getActiveJob(platform) {
  const job = state.jobs[platform];
  return (job && job.status === "running") ? job : null;
}

function getRunningJobs() {
  return Object.values(state.jobs).filter(j => j.status === "running");
}

function cancelAnalysisJob(platform) {
  const job = state.jobs[platform];
  if (job && job.status === "running") {
    job.status = "cancelled";
    if (job.abortController) {
      job.abortController.abort();
    }
    if (job.timerId) {
      clearInterval(job.timerId);
    }
    job.progressText = "Análisis cancelado por el usuario";
    showToast(`Análisis de ${platformLabel(platform)} cancelado`);
    renderActiveView();
    updateNotch();
  }
}

function renderJobBanners() {
  const platforms = ["instagram", "facebook", "tiktok", "x", "global"];
  platforms.forEach(p => {
    const banner = document.getElementById(`${p}JobBanner`);
    const progressText = document.getElementById(`${p}JobProgressText`);
    const job = state.jobs[p];

    if (!banner) return;

    if (job && job.status === "running") {
      banner.hidden = false;
      if (progressText) progressText.textContent = job.progressText;
    } else {
      banner.hidden = true;
    }
  });
}

function startAnalysisJob({ targetPlatform = "global", notify = false, force = false } = {}) {
  // If a job for targetPlatform is ALREADY running, do NOT restart it or abort it!
  const existingJob = getActiveJob(targetPlatform);
  if (existingJob) {
    showToast(`El análisis de ${platformLabel(targetPlatform)} ya se encuentra en ejecución (${existingJob.elapsedSeconds}s)`);
    renderActiveView();
    return existingJob;
  }

  clearSourceErrors();

  let platformsToTest = targetPlatform === "global" ? ["instagram", "facebook", "tiktok", "x"] : [targetPlatform];

  const validations = platformsToTest.map(platform => {
    const raw = (state.sources[platform] || "").trim();
    return { platform, ...validateSourceUrl(platform, raw) };
  });

  const invalid = validations.filter(item => item.status === "invalid");
  const assignedPlatforms = validations
    .filter(item => item.status === "ok")
    .sort((left, right) => Number(isDirectPostUrl(right.platform, right.url)) - Number(isDirectPostUrl(left.platform, left.url)));

  invalid.forEach(item => setSourceError(item.platform, item.message));

  if (!assignedPlatforms.length) {
    if (invalid.length && notify) showToast("URLs incorrectas. Verifica los campos.");
    return null;
  }

  const job = {
    id: `job_${targetPlatform}_${Date.now()}`,
    platform: targetPlatform,
    assignedPlatforms: assignedPlatforms,
    prompt: state.prompt.trim(),
    force: force,
    status: "running",
    startedAt: Date.now(),
    elapsedSeconds: 0,
    progressText: `Iniciando consulta de ${platformLabel(targetPlatform)}... 0s`,
    abortController: new AbortController(),
    timerId: null,
    results: null
  };

  state.jobs[targetPlatform] = job;

  job.timerId = setInterval(() => {
    if (job.status !== "running") {
      clearInterval(job.timerId);
      return;
    }
    job.elapsedSeconds = Math.round((Date.now() - job.startedAt) / 1000);
    renderJobBanners();
    updateNotch();
  }, 1000);

  renderActiveView();
  updateNotch();

  // Run asynchronously in the background independently of screen navigation!
  executeJobAsync(job);

  return job;
}

async function executeJobAsync(job) {
  const { signal } = job.abortController;
  let mergedComments = [...state.comments];
  let messages = [];
  let anyLive = false;

  try {
    for (const item of job.assignedPlatforms) {
      if (signal.aborted || job.status === "cancelled") {
        job.status = "cancelled";
        break;
      }

      job.progressText = `Consultando ${platformLabel(item.platform)}... ${job.elapsedSeconds}s`;
      renderJobBanners();
      updateNotch();

      try {
        const dashboard = await fetchPlatformComments(item, {
          force: job.force,
          signal: signal,
          prompt: job.prompt
        });

        if (signal.aborted || job.status === "cancelled") {
          job.status = "cancelled";
          break;
        }

        const platformComments = (dashboard.comments || [])
          .map(comment => ({ ...comment, platform: normalizePlatform(comment.platform || item.platform) }))
          .filter(c => normalizePlatform(c.platform) === normalizePlatform(item.platform));
        // Merge against the latest shared state. Multiple platform jobs may
        // finish in a different order while the user navigates between views.
        mergedComments = [
          ...state.comments.filter(c => normalizePlatform(c.platform) !== normalizePlatform(item.platform)),
          ...platformComments
        ];

        if (dashboard.dataSource === "live" && platformComments.length) anyLive = true;
        if (dashboard.message) messages.push(`${platformLabel(item.platform)}: ${dashboard.message}`);

        state.comments = mergedComments;
        state.summary = null;
        state.dataSource = anyLive ? "live" : (dashboard.dataSource || "demo");

        renderActiveView();

      } catch (err) {
        if (err.name === "AbortError" || signal.aborted || job.status === "cancelled") {
          job.status = "cancelled";
          break;
        }
        const msg = err.message || `Error en ${platformLabel(item.platform)}`;
        messages.push(`${platformLabel(item.platform)}: ${msg}`);
        setSourceError(item.platform, msg);
        renderActiveView();
      }
    }

    if (job.timerId) clearInterval(job.timerId);

    if (job.status === "cancelled" || signal.aborted) {
      job.status = "cancelled";
      job.progressText = "Análisis cancelado por el usuario";
      renderJobBanners();
      updateNotch();
      return;
    }

    job.status = "completed";
    job.progressText = `Análisis de ${platformLabel(job.platform)} completado`;
    const jobPlatforms = new Set(job.assignedPlatforms.map(item => item.platform));
    const jobComments = job.platform === "global"
      ? [...state.comments]
      : state.comments.filter(comment => jobPlatforms.has(comment.platform));
    job.results = {
      comments: jobComments,
      summary: null,
      dataSource: anyLive ? "live" : "demo",
      message: messages.join(" · ")
    };

    saveHistoryRecord(job.results, job.assignedPlatforms, job.prompt);

    // Discreet Completion Notification when user is on a DIFFERENT view:
    const isCurrentModuleView = (state.activeNav === job.platform) || (state.activeNav === "global" && job.platform === "global");
    if (!isCurrentModuleView) {
      showToast(`✦ Análisis de ${platformLabel(job.platform)} finalizado (${jobComments.length} comentarios)`);
    } else {
      showToast(anyLive ? `Actualizado (${jobComments.length} comentarios)` : "Datos demostrativos cargados");
    }

    renderActiveView();
    updateNotch();

  } catch (err) {
    if (job.timerId) clearInterval(job.timerId);
    if (err.name === "AbortError" || signal.aborted || job.status === "cancelled") {
      job.status = "cancelled";
    } else {
      job.status = "error";
      job.error = err.message || "Error al procesar la consulta";
      showToast(`Error en análisis de ${platformLabel(job.platform)}: ${job.error}`);
    }
    renderActiveView();
    updateNotch();
  }
}

function loadComments({ notify = false, force = false, platform = null } = {}) {
  const target = platform || (["instagram", "facebook", "tiktok", "x"].includes(state.activeNav) ? state.activeNav : "global");
  return startAnalysisJob({ targetPlatform: target, notify, force });
}

/* ========================================== */
/* UI SYNC HELPERS & EVENT BINDINGS           */
/* ========================================== */
function syncSourceInput(platform, value) {
  state.sources[platform] = value;
  const inputs = [
    document.getElementById(`${platform}SourceInput`),
    document.getElementById(`global${platform.charAt(0).toUpperCase() + platform.slice(1)}Input`)
  ];
  inputs.forEach(el => { if (el && el.value !== value) el.value = value; });
  try { localStorage.setItem("sentia-global-sources-v1", JSON.stringify(state.sources)); } catch (e) {}
}

function syncPromptInputs(value) {
  state.prompt = value;
  const inputs = [
    document.getElementById("promptInput"),
    document.getElementById("instagramPromptInput"),
    document.getElementById("facebookPromptInput"),
    document.getElementById("tiktokPromptInput"),
    document.getElementById("xPromptInput")
  ];
  inputs.forEach(el => { if (el && el.value !== value) el.value = value; });
}

function loadSavedSources() {
  try {
    const saved = JSON.parse(localStorage.getItem("sentia-global-sources-v1") || "{}");
    for (const platform of ["instagram", "facebook", "tiktok", "x"]) {
      if (typeof saved[platform] === "string") {
        syncSourceInput(platform, saved[platform]);
      }
    }
  } catch (e) {}
}

function clearSourceErrors() {
  document.querySelectorAll(".source-error").forEach(el => {
    el.hidden = true;
    el.textContent = "";
  });
}

function setSourceError(platform, message) {
  const ids = [`${platform}SourceError`, `global${platform.charAt(0).toUpperCase() + platform.slice(1)}Error`];
  ids.forEach(id => {
    const el = document.getElementById(id);
    if (el) {
      el.hidden = false;
      el.textContent = message;
    }
  });
}

function showToast(message) {
  const toast = document.getElementById("toast");
  if (!toast) return;
  toast.textContent = message;
  toast.classList.add("visible");
  window.clearTimeout(showToast.timeout);
  showToast.timeout = window.setTimeout(() => toast.classList.remove("visible"), 2800);
}

function downloadCsv(csvContent, filename) {
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob(["\ufeff", csvContent], { type: "text/csv;charset=utf-8" }));
  link.download = filename;
  link.click();
  URL.revokeObjectURL(link.href);
}

function initEventHandlers() {
  // Source inputs binding
  ["instagram", "facebook", "tiktok", "x"].forEach(platform => {
    const singleInput = document.getElementById(`${platform}SourceInput`);
    const globalInput = document.getElementById(`global${platform.charAt(0).toUpperCase() + platform.slice(1)}Input`);

    [singleInput, globalInput].forEach(input => {
      if (!input) return;
      input.addEventListener("input", e => syncSourceInput(platform, e.target.value));
    });

    const singleBtn = document.querySelector(`.analyze-single-btn[data-platform="${platform}"]`);
    if (singleBtn) {
      singleBtn.addEventListener("click", () => {
        navigateTo(platform);
        startAnalysisJob({ targetPlatform: platform, notify: true, force: false });
      });
    }
  });

  // Cancel Job Buttons Binding
  document.querySelectorAll(".cancel-job-button").forEach(btn => {
    btn.addEventListener("click", () => {
      const platform = btn.dataset.cancelPlatform;
      cancelAnalysisJob(platform);
    });
  });

  // Prompt inputs binding
  ["promptInput", "instagramPromptInput", "facebookPromptInput", "tiktokPromptInput", "xPromptInput"].forEach(id => {
    const input = document.getElementById(id);
    if (input) {
      input.addEventListener("input", e => syncPromptInputs(e.target.value));
    }
  });

  // Analyze Global Button
  const analyzeBtn = document.getElementById("analyzeButton");
  if (analyzeBtn) {
    analyzeBtn.addEventListener("click", () => loadComments({ notify: true, force: false }));
  }

  // Refresh Header Button
  const refreshBtn = document.getElementById("refreshButton");
  if (refreshBtn) {
    refreshBtn.addEventListener("click", () => loadComments({ notify: true, force: true }));
  }

  // Search & Filter Listeners for per-module and global tables
  document.querySelectorAll(".table-search-input").forEach(input => {
    input.addEventListener("input", () => renderActiveView());
  });

  document.querySelectorAll(".sentiment-filter-select").forEach(select => {
    select.addEventListener("change", () => renderActiveView());
  });

  document.querySelectorAll(".sort-filter-select").forEach(select => {
    select.addEventListener("change", () => renderActiveView());
  });

  const globalPlatformSelect = document.getElementById("globalPlatformSelect");
  if (globalPlatformSelect) {
    globalPlatformSelect.addEventListener("change", () => renderGlobalView());
  }

  // CSV Export Buttons
  document.querySelectorAll(".export-button").forEach(btn => {
    btn.addEventListener("click", () => {
      const platform = btn.dataset.platform || (state.activeNav === "global" ? "global" : state.activeNav);
      const comments = filterComments(platform, state.sentiment, state.query, state.sort);
      
      const rows = [
        ["Red", "Usuario", "Comentario", "Publicación", "Emoción", "Confianza", "Fecha"],
        ...comments.map(item => [item.platform, item.authorName, item.text, item.postTitle, item.sentiment.displayName, `${item.sentiment.confidence}%`, item.publishedAt])
      ];

      const csv = rows.map(r => r.map(v => `"${String(v).replaceAll('"', '""')}"`).join(",")).join("\n");
      downloadCsv(csv, `sentia-global-${platform}-${Date.now()}.csv`);
      showToast(`Exportados ${comments.length} comentarios de ${platformLabel(platform)}`);
    });
  });

  // History Clear & Export Log Buttons
  const clearHistoryBtn = document.getElementById("clearHistoryBtn");
  if (clearHistoryBtn) clearHistoryBtn.addEventListener("click", clearAllHistory);

  const exportHistoryLogBtn = document.getElementById("exportHistoryLogBtn");
  if (exportHistoryLogBtn) exportHistoryLogBtn.addEventListener("click", exportFullHistoryLog);

  // Close History Modal
  const closeHistoryModalBtn = document.getElementById("closeHistoryModalBtn");
  const historyModalCloseBtn = document.getElementById("historyModalCloseBtn");
  const historyDetailDialog = document.getElementById("historyDetailDialog");

  if (closeHistoryModalBtn) closeHistoryModalBtn.addEventListener("click", () => historyDetailDialog.close());
  if (historyModalCloseBtn) historyModalCloseBtn.addEventListener("click", () => historyDetailDialog.close());

  // Sidebar Collapse Toggle Listener
  const sidebarCollapseToggle = document.getElementById("sidebarCollapseToggle");
  if (sidebarCollapseToggle) {
    sidebarCollapseToggle.addEventListener("click", toggleSidebarCollapse);
  }

  // Credentials Forms
  const apiKeysDialog = document.getElementById("apiKeysDialog");
  const closeApiKeysButton = document.getElementById("closeApiKeysButton");
  const cancelApiKeysButton = document.getElementById("cancelApiKeysButton");
  const apiKeysForm = document.getElementById("apiKeysForm");
  const settingsForm = document.getElementById("settingsPageApiKeysForm");

  if (closeApiKeysButton) closeApiKeysButton.addEventListener("click", () => apiKeysDialog.close());
  if (cancelApiKeysButton) cancelApiKeysButton.addEventListener("click", () => apiKeysDialog.close());

  if (apiKeysForm) {
    apiKeysForm.addEventListener("submit", async e => {
      e.preventDefault();
      try {
        const payload = buildCredentialPayload(new FormData(apiKeysForm));
        await saveApiKeysPayload(payload);
        apiKeysDialog.close();
      } catch (err) {
        showToast(err.message || "Error al guardar credenciales");
      }
    });
  }

  if (settingsForm) {
    settingsForm.addEventListener("submit", async e => {
      e.preventDefault();
      try {
        const payload = buildCredentialPayload(new FormData(settingsForm));
        await saveApiKeysPayload(payload);
      } catch (err) {
        showToast(err.message || "Error al guardar credenciales");
      }
    });
  }

  // Theme Buttons
  document.querySelectorAll(".theme-btn").forEach(btn => {
    btn.addEventListener("click", () => setTheme(btn.dataset.themeVal));
  });
}

function initializeFluentSelects() {
  document.querySelectorAll(".fluent-select").forEach(select => {
    const trigger = select.querySelector(".select-trigger");
    const popover = select.querySelector(".select-popover");
    const options = [...select.querySelectorAll(".select-option")];

    if (!trigger || !popover) return;

    trigger.addEventListener("click", () => {
      const isOpen = select.classList.contains("open");
      document.querySelectorAll(".fluent-select.open").forEach(s => s.classList.remove("open"));
      if (!isOpen) {
        select.classList.add("open");
        popover.hidden = false;
      } else {
        popover.hidden = true;
      }
    });

    options.forEach(option => {
      option.addEventListener("click", () => {
        const val = option.dataset.value;
        select.querySelectorAll(".select-option").forEach(o => o.setAttribute("aria-selected", String(o === option)));
        select.querySelector("[data-select-label]").textContent = option.querySelector("span").textContent;
        
        if (select.dataset.select === "periodDays") {
          state.periodDays = Number(val);
          renderActiveView();
        } else if (select.dataset.select === "sentiment") {
          state.sentiment = val;
          renderActiveView();
        } else if (select.dataset.select === "sort") {
          state.sort = val;
          renderActiveView();
        }

        select.classList.remove("open");
        popover.hidden = true;
      });
    });
  });

  document.addEventListener("click", event => {
    if (!event.target.closest(".fluent-select")) {
      document.querySelectorAll(".fluent-select.open").forEach(s => {
        s.classList.remove("open");
        const pop = s.querySelector(".select-popover");
        if (pop) pop.hidden = true;
      });
    }
  });
}

/* ========================================== */
/* SIDEBAR COLLAPSE TOGGLE (DESKTOP)          */
/* ========================================== */
function initSidebarCollapse() {
  const saved = localStorage.getItem("sentia-sidebar-collapsed");
  const isCollapsed = saved === "true";
  setSidebarCollapse(isCollapsed);
}

function setSidebarCollapse(collapsed) {
  const sidebar = document.getElementById("appSidebar");
  const wrapper = document.querySelector(".app-main-wrapper");
  const toggleBtn = document.getElementById("sidebarCollapseToggle");

  if (!sidebar) return;

  if (collapsed) {
    sidebar.classList.add("collapsed");
    if (wrapper) wrapper.classList.add("sidebar-collapsed");
    if (toggleBtn) {
      toggleBtn.setAttribute("aria-expanded", "false");
      toggleBtn.setAttribute("aria-label", "Expandir menú lateral");
      toggleBtn.title = "Expandir menú lateral";
    }
  } else {
    sidebar.classList.remove("collapsed");
    if (wrapper) wrapper.classList.remove("sidebar-collapsed");
    if (toggleBtn) {
      toggleBtn.setAttribute("aria-expanded", "true");
      toggleBtn.setAttribute("aria-label", "Colapsar menú lateral");
      toggleBtn.title = "Colapsar menú lateral";
    }
  }

  try {
    localStorage.setItem("sentia-sidebar-collapsed", collapsed ? "true" : "false");
  } catch (e) {}
}

function toggleSidebarCollapse() {
  const sidebar = document.getElementById("appSidebar");
  const isCurrentlyCollapsed = sidebar ? sidebar.classList.contains("collapsed") : false;
  setSidebarCollapse(!isCurrentlyCollapsed);
}

/* ========================================== */
/* APP INITIALIZATION ENTRY POINT             */
/* ========================================== */
document.addEventListener("DOMContentLoaded", () => {
  initTheme();
  initNavigation();
  initSidebarCollapse();
  initEventHandlers();
  initializeFluentSelects();
  loadSavedSources();
  loadHistory();
  refreshCredentialStatus();
  
  // Initial load
  loadComments();
});
