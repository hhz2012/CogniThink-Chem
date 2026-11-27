/* CogniThink-Chem 化学伴思 · 前端演示站逻辑
   页面内容全部来自 data/problems.json（由推断引擎 --export-web 真实运行生成）。 */
'use strict';

const DIFFICULTY = { Basic: '基础', Medium: '中档', Hard: '挑战' };

const STATUS = {
  Solved: { text: '已解出', cls: 'ok' },
  Underdetermined: { text: '欠定', cls: 'warn' },
  NoSolution: { text: '无解', cls: 'err' },
};

const COEXIST_TEXT = {
  Coexist: '能大量共存',
  NotCoexist: '不能大量共存',
  Underdetermined: '无法判断',
};

const COMPOSITION_LABEL = { Must: '一定存在', MustNot: '一定不存在', Maybe: '可能存在' };

/* 离子粉末推断三态结论表列名（高考表述） */
const IONP_LABEL = { Must: '一定存在', MustNot: '一定不存在', Maybe: '不能确定' };

/* 实验探究模式中文名 */
const EXP_MODE_TEXT = {
  gas_test: '气体验验',
  gas_full_test: '气体验满',
  operation_purpose: '操作目的与误差分析',
  phenomenon_conclusion: '现象→结论',
};

const DIRECTION_TEXT = { forward: '正向移动', reverse: '逆向移动', noShift: '平衡不移动' };

/* 电化学装置类型中文名 */
const CELL_TYPE_TEXT = { galvanic: '原电池', electrolytic: '电解池' };

/* 平衡题给信息种类中文名 */
const EQ_KNOWN_TEXT = { conversion: '转化率', amountFraction: '平衡分数', moles: '平衡物质的量' };

/* 平衡扰动种类中文名 */
const DISTURB_TEXT = {
  addSpecies: '加料',
  compress: '缩小体积加压',
  expand: '减压',
  heatUp: '升温',
  coolDown: '降温',
  catalyst: '加催化剂',
};

const FACT_KIND = {
  Mass: '质量',
  Moles: '物质的量',
  GasVolume: '气体体积（标况）',
  SolutionVolume: '溶液体积',
  Concentration: '浓度',
  ParticleCount: '粒子数',
  ActualYieldMass: '实际产量',
};

let DATA = null;
let KB = null;
let activeCategory = 'All';
let keyword = '';
let activeView = 'problems';

init();

async function init() {
  // 仅在主应用页面（index.html）执行完整初始化；演示页等跳过
  if (!document.getElementById('loadingTip')) return;

  try {
    const [problemsRes, knowledgeRes] = await Promise.all([
      fetch('data/problems.json'),
      fetch('data/knowledge.json'),
    ]);
    if (!problemsRes.ok) throw new Error('problems.json HTTP ' + problemsRes.status);
    DATA = await problemsRes.json();
    if (knowledgeRes.ok) {
      KB = await knowledgeRes.json();
    } else {
      KB = { items: [], rules: [] };
    }
  } catch (err) {
    showError('加载数据失败（' + err.message +
      '）。请通过 HTTP 服务访问本页（如在仓库根目录运行 python -m http.server 后打开 /web/），或直接访问 GitHub Pages。');
    return;
  }

  document.getElementById('loadingTip').hidden = true;
  renderStats();
  renderCatTabs();
  renderFooter();

  document.getElementById('searchBox').addEventListener('input', (e) => {
    keyword = e.target.value.trim().toLowerCase();
    if (activeView === 'knowledge') renderKnowledgeList();
    else renderList();
  });

  // 主导航：题目 / 知识库 / 问答
  document.querySelectorAll('.nav-btn').forEach((btn) => {
    btn.addEventListener('click', () => switchView(btn.dataset.view));
  });

  window.addEventListener('hashchange', route);
  route();
}

/* ---------- 文本工具 ---------- */

function esc(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/* 轻量化学式排版（在 HTML 转义之后应用）：
   ^电荷 → 上标（Fe^3+ → Fe³⁺）；字母或右括号后的数字 → 下标（H2O、Ca(OH)2）。 */
function chem(s) {
  let t = esc(s);
  t = t.replace(/\^(\d+[+\-]|[+\-]\d+|\d+|[+\-])/g, '<sup>$1</sup>');
  t = t.replace(/([A-Za-z)])(\d+)/g, '$1<sub>$2</sub>');
  return t;
}

/* ---------- 头部与工具栏 ---------- */

function renderStats() {
  document.getElementById('stats').innerHTML = `
    <div class="stat"><b>${DATA.total}</b><span>道基准题</span></div>
    <div class="stat"><b>${DATA.categories.length}</b><span>类题型全覆盖</span></div>
    <div class="stat"><b>${DATA.knowledgeCount}</b><span>条知识点可溯源</span></div>`;
}

function renderCatTabs() {
  const tabs = [{ category: 'All', name: '全部', count: DATA.total }]
    .concat(DATA.categories);
  document.getElementById('catTabs').innerHTML = tabs.map((c) => `
    <button class="cat-tab${c.category === activeCategory ? ' active' : ''}" data-cat="${esc(c.category)}">
      ${esc(c.name)}<span class="count">${c.count}</span>
    </button>`).join('');

  document.querySelectorAll('.cat-tab').forEach((btn) => {
    btn.addEventListener('click', () => {
      activeCategory = btn.dataset.cat;
      document.querySelectorAll('.cat-tab').forEach((b) =>
        b.classList.toggle('active', b === btn));
      if (location.hash.replace(/^#\/?/, '')) {
        location.hash = '';
      }
      renderList();
    });
  });
}

function renderFooter() {
  document.getElementById('footerInfo').textContent =
    `${DATA.engine.displayName}（${DATA.engine.name} v${DATA.engine.version}）` +
    ` · 数据生成于 ${DATA.generatedAt} · 全部解题过程、讲解与引导由知识驱动推断引擎真实运行生成`;
}

/* ---------- 视图切换 ---------- */

function switchView(view) {
  activeView = view;
  document.querySelectorAll('.nav-btn').forEach((b) =>
    b.classList.toggle('active', b.dataset.view === view));
  document.getElementById('listView').hidden = view !== 'problems';
  document.getElementById('detailView').hidden = true;
  document.getElementById('vizView').hidden = view !== 'viz';
  document.getElementById('knowledgeView').hidden = view !== 'knowledge';
  document.getElementById('qaView').hidden = view !== 'qa';
  document.getElementById('liveView').hidden = view !== 'live';
  document.getElementById('simView').hidden = view !== 'sim';
  document.querySelector('.toolbar').style.display = (view === 'problems' || view === 'knowledge') ? '' : 'none';
  document.getElementById('catTabs').style.display = view === 'problems' ? '' : 'none';

  if (view === 'problems') {
    location.hash = '';
    showList();
  } else if (view === 'viz') {
    location.hash = '#viz';
    renderVizGallery();
  } else if (view === 'knowledge') {
    location.hash = '#knowledge';
    renderKnowledgePage();
  } else if (view === 'qa') {
    location.hash = '#qa';
    renderQAPanel();
  } else if (view === 'live') {
    location.hash = '#live';
    renderLivePanel();
  } else if (view === 'sim') {
    location.hash = '#sim';
    renderSimPanel();
  }
}

function route() {
  if (!DATA) return;
  const hash = location.hash.replace(/^#\/?/, '');
  if (hash === 'knowledge') { switchView('knowledge'); return; }
  if (hash === 'qa') { switchView('qa'); return; }
  if (hash === 'viz') { switchView('viz'); return; }
  if (hash === 'live') { switchView('live'); return; }
  if (hash === 'sim') { switchView('sim'); return; }
  switchView('problems');
  const rec = hash ? DATA.problems.find((r) => r.problem.id === hash) : null;
  if (rec) showDetail(rec);
  else showList();
}

function showList() {
  document.getElementById('detailView').hidden = true;
  document.getElementById('listView').hidden = false;
  renderList();
}

function renderList() {
  const list = DATA.problems.filter((r) => {
    const p = r.problem;
    if (activeCategory !== 'All' && p.category !== activeCategory) return false;
    if (keyword) {
      const hay = (p.id + ' ' + p.problemText + ' ' + p.tags.join(' ')).toLowerCase();
      if (!hay.includes(keyword)) return false;
    }
    return true;
  });

  document.getElementById('emptyTip').hidden = list.length > 0;
  document.getElementById('problemGrid').innerHTML = list.map(cardHtml).join('');
}

function cardHtml(r) {
  const p = r.problem;
  const s = r.solution;
  const st = STATUS[s.status] || { text: s.status, cls: 'warn' };
  return `
  <a class="card cat-${esc(p.category)}" href="#/${esc(p.id)}">
    <div class="card-top">
      <span class="badge cat-badge">${esc(p.categoryName)}</span>
      <span class="badge diff-badge">${esc(DIFFICULTY[p.difficulty] || p.difficulty)}</span>
      <span class="status-dot st-${st.cls}" title="${esc(st.text)}"></span>
    </div>
    <h3 class="card-title">${esc(p.id)}</h3>
    <p class="card-text">${esc(p.problemText)}</p>
    <div class="card-foot">
      <span class="mono">${esc(st.text)}</span>
      <span class="more">查看 解 / 讲 / 引导 →</span>
    </div>
  </a>`;
}

/* ---------- 可视化效果一览 ---------- */

/* 汇总各类可视化：直接从真实题目数据实时渲染，作为演示入口 */
function vizCatalog() {
  const byId = (id) => DATA.problems.find((r) => r.problem.id === id) || null;
  const items = [];
  const add = (rec, title, desc, html) => {
    if (rec && html) items.push({ rec, title, desc, html });
  };

  const elec = byId('ELEC-H01');
  if (elec) {
    add(elec, '电化学装置图', '电极材料、电子流向、离子迁移与半反应',
      electrochemDiagramHtml(elec.problem.given.cell, elec.solution.electrodes));
  }

  const proc = byId('PROC-001');
  if (proc) {
    let html = processFlowHtml(proc.problem.given.steps, proc.solution.substanceMap, null);
    if (typeof $3Dmol !== 'undefined') {
      html += moleculeViewerHtml(proc.solution.molBlocks, proc.solution.substanceMap);
    }
    add(proc, '转化链 + 3D 分子', 'A→B→C 转化关系与可拖拽旋转的分子结构', html);
  }

  const coex = byId('COEX-001');
  if (coex) {
    add(coex, '离子共存冲突图', '冲突离子对按原因着色，中心给出共存结论',
      coexistenceGraphHtml(coex.solution.conflicts, coex.problem.given.species, coex.problem.given.medium));
  }

  const calc = byId('CALC-001');
  if (calc) {
    add(calc, '物质的量换算关系', 'n 与 m / N / V / c 的换算路径',
      moleRelationHtml(calc.solution.numericValue, calc.solution.numericUnit, calc.solution.numericSpecies));
  }

  const eq = byId('EQ-H01');
  if (eq) {
    add(eq, '浓度-时间曲线', '各物种指数趋近平衡的过程示意',
      concentrationTimeHtml(eq.solution.equilibrium));
  }

  const aq = byId('AQ-H01');
  if (aq) {
    add(aq, '粒子浓度相对大小', '按浓度由大到小排序的柱状图',
      concentrationBarsHtml(aq.solution.aqueous?.order));
  }

  const exp = byId('EXP-H05');
  if (exp) {
    add(exp, '实验装置示意图', '蒸馏、萃取分液等装置结构',
      experimentApparatusHtml(exp.problem.given.experiment, ''));
  }

  if (proc) {
    add(proc, '原子守恒校验', '逐元素核对方程式两侧原子数',
      atomBalanceHtml(proc.solution.equations.slice(0, 1)));
  }

  return items;
}

function renderVizGallery() {
  const view = document.getElementById('vizView');
  const items = vizCatalog();
  view.innerHTML = `
    <div class="viz-hero">
      <h2>可视化效果一览</h2>
      <p>图示均由题目数据与推断结果实时生成${typeof $3Dmol !== 'undefined' ? '，分子结构支持拖拽旋转' : ''}。点击卡片可进入对应题目。</p>
    </div>
    <div class="viz-gallery">
      ${items.map((it) => `
        <article class="viz-card">
          <header class="viz-card-head">
            <h3>${esc(it.title)}</h3>
            <p>${esc(it.desc)}</p>
          </header>
          <div class="viz-card-body">${it.html}</div>
          <a class="viz-card-foot" href="#/${esc(it.rec.problem.id)}" data-open="${esc(it.rec.problem.id)}">
            ${esc(it.rec.problem.categoryName)} · ${esc(it.rec.problem.id)}<span>查看题目 →</span>
          </a>
        </article>`).join('')}
    </div>`;
  // 从展示页进入题目详情（避免 hash 跳转把详情页重置回列表）
  view.querySelectorAll('[data-open]').forEach((a) => {
    a.addEventListener('click', (ev) => {
      ev.preventDefault();
      const rec = DATA.problems.find((r) => r.problem.id === a.dataset.open);
      if (!rec) return;
      activeView = 'problems';
      document.querySelectorAll('.nav-btn').forEach((b) =>
        b.classList.toggle('active', b.dataset.view === 'problems'));
      document.getElementById('vizView').hidden = true;
      history.replaceState(null, '', '#/' + encodeURIComponent(rec.problem.id));
      showDetail(rec);
      window.scrollTo({ top: 0 });
    });
  });
  setTimeout(initMoleculeViewers, 60);
  window.scrollTo({ top: 0 });
}

/* ---------- 详情视图 ---------- */

function showDetail(rec) {
  document.getElementById('listView').hidden = true;
  const view = document.getElementById('detailView');
  view.hidden = false;
  view.innerHTML = detailHtml(rec);
  bindTabs(view);
  // 3Dmol 分子查看器需要在 DOM 渲染后初始化
  setTimeout(initMoleculeViewers, 50);
  window.scrollTo({ top: 0 });
}

function detailHtml(r) {
  const p = r.problem;
  const s = r.solution;
  const e = r.explanation;
  const g = r.guide;
  const st = STATUS[s.status] || { text: s.status, cls: 'warn' };
  const tags = p.tags.length ? '｜标签：' + p.tags.map(esc).join('、') : '';

  return `
  <a class="back" href="#">← 返回题目列表</a>
  <article class="detail cat-${esc(p.category)}">
    <header class="detail-head">
      <div class="badges">
        <span class="badge cat-badge">${esc(p.categoryName)}</span>
        <span class="badge diff-badge">${esc(DIFFICULTY[p.difficulty] || p.difficulty)}</span>
        <span class="badge st-badge st-${st.cls}">${esc(st.text)}</span>
      </div>
      <h2>${esc(p.id)}</h2>
      <p class="problem-text">${chem(p.problemText)}</p>
      <p class="meta">出处：${esc(p.source)}${tags}</p>
    </header>

    ${givenHtml(p)}

    <section class="answer-card">
      <h4>答案小结（引擎生成）</h4>
      <p>${chem(e.answerSummary)}</p>
    </section>

    <nav class="tabs" role="tablist">
      <button class="tab active" data-tab="solve" role="tab">解 · 求解过程</button>
      <button class="tab" data-tab="explain" role="tab">讲 · 逐步讲解</button>
      <button class="tab" data-tab="guide" role="tab">引导 · 伴思提问</button>
    </nav>

    <section class="panel" id="panel-solve">${solveHtml(s, p.category, p.given)}</section>
    <section class="panel" id="panel-explain" hidden>${explainHtml(e)}</section>
    <section class="panel" id="panel-guide" hidden>${guideHtml(g)}</section>
  </article>`;
}

function bindTabs(view) {
  view.querySelectorAll('.tab').forEach((btn) => {
    btn.addEventListener('click', () => {
      view.querySelectorAll('.tab').forEach((b) => b.classList.toggle('active', b === btn));
      ['solve', 'explain', 'guide'].forEach((k) => {
        view.querySelector('#panel-' + k).hidden = btn.dataset.tab !== k;
      });
    });
  });
}

/* 结构化已知条件（引擎输入） */
function givenHtml(p) {
  const g = p.given;
  const rows = [];

  if (g.species.length) rows.push(['反应物 / 离子组', g.species.map(chem).join('、')]);
  if (g.conditions.length) rows.push(['反应条件', g.conditions.map(esc).join('、')]);
  if (g.medium) {
    rows.push(['介质', g.medium === 'acidic' ? '酸性' : g.medium === 'basic' ? '碱性' : esc(g.medium)]);
  }
  if (g.candidateSpecies.length) rows.push(['候选物质', g.candidateSpecies.map(chem).join('、')]);
  if (g.singleEntity) rows.push(['模式', '单一实体（候选中的某一种）']);

  const steps = g.steps.map((st) => {
    const parts = [`步${st.step}：${chem(st.from)} → ${chem(st.to)}`];
    if (st.reagents.length) parts.push('加入 ' + st.reagents.map(chem).join('、'));
    if (st.conditions.length) parts.push('条件 ' + st.conditions.map(esc).join('、'));
    if (st.phenomenon) parts.push('现象：' + esc(st.phenomenon));
    if (st.purpose) parts.push('操作：' + chem(st.purpose));
    return `<li>${parts.join('｜')}</li>`;
  }).join('');
  if (steps) rows.push(['过程步骤', `<ol class="given-steps">${steps}</ol>`]);

  const facts = g.numericFacts.map((f) =>
    `<li>${f.species ? chem(f.species) + ' 的' : ''}${esc(FACT_KIND[f.kind] || f.kind)} = ${esc(f.value)} ${esc(f.unit)}</li>`
  ).join('');
  if (facts) rows.push(['已知数据', `<ul class="given-steps">${facts}</ul>`]);

  if (g.experiment) {
    const ex = g.experiment;
    const cells = [];
    if (ex.targets.length) cells.push('询问对象：' + ex.targets.map(chem).join('、'));
    if (ex.operations.length) cells.push('操作代号：' + ex.operations.map(esc).join('、'));
    if (ex.phenomena.length) cells.push('观察现象：' + ex.phenomena.map(esc).join('、'));
    if (ex.candidateSpecies.length) cells.push('候选物种：' + ex.candidateSpecies.map(chem).join('、'));
    rows.push(['探究模式',
      esc(EXP_MODE_TEXT[ex.mode] || ex.mode) +
      (cells.length ? `<ul class="given-steps">${cells.map((c) => `<li>${c}</li>`).join('')}</ul>` : '')]);
  }

  /* 电化学装置（电化学题）：字段为空/缺省时不产生对应行 */
  if (g.cell) {
    const c = g.cell;
    const cells = [];
    cells.push('类型：' + esc(CELL_TYPE_TEXT[c.type] || c.type));
    if (c.electrodes.length) cells.push('电极：' + c.electrodes.map(chem).join('、'));
    if (c.electrolyte) cells.push('电解质：' + chem(c.electrolyte));
    if (c.medium) {
      cells.push('介质：' + (c.medium === 'acidic' ? '酸性' : c.medium === 'basic' ? '碱性' : esc(c.medium)));
    }
    if (c.overall) cells.push('总反应：' + chem(c.overall));
    if (c.gasFeeds.length) cells.push('通入气体：' + c.gasFeeds.map(chem).join('、'));
    rows.push(['电化学装置', `<ul class="given-steps">${cells.map((x) => `<li>${x}</li>`).join('')}</ul>`]);
  }

  /* 平衡体系（化学平衡综合题） */
  if (g.equilibrium) {
    const q = g.equilibrium;
    const cells = [];
    if (q.equation) cells.push('可逆反应：' + chem(q.equation));
    if (q.container) {
      cells.push('容器：' + esc(q.container === 'constantVolume' ? '恒温恒容'
        : q.container === 'constantPressure' ? '恒温恒压' : q.container));
    }
    const initial = Object.keys(q.initial).sort().map((k) => `${chem(k)} ${esc(q.initial[k])} mol`);
    if (initial.length) cells.push('起始投料：' + initial.join('、'));
    if (q.known) {
      const kn = q.known;
      cells.push('题给平衡信息：' + chem(kn.species) + ' 的' + esc(EQ_KNOWN_TEXT[kn.kind] || kn.kind) +
        ' = ' + esc(kn.value) + (kn.kind === 'moles' ? ' mol' : ''));
    }
    if (q.disturbance) {
      const d = q.disturbance;
      let txt = esc(DISTURB_TEXT[d.kind] || d.kind);
      if (d.species) txt += '（' + chem(d.species) + '）';
      if (d.thermal) {
        txt += '，正反应' + esc(d.thermal === 'exothermic' ? '放热' : d.thermal === 'endothermic' ? '吸热' : d.thermal);
      }
      cells.push('扰动：' + txt);
    }
    if (q.totalPressure) cells.push('体系总压：' + esc(q.totalPressure));
    if (q.fractionSpecies) cells.push('求摩尔分数物种：' + chem(q.fractionSpecies));
    if (cells.length) rows.push(['平衡体系', `<ul class="given-steps">${cells.map((x) => `<li>${x}</li>`).join('')}</ul>`]);
  }

  /* 溶液组成（水溶液离子平衡题） */
  if (g.solution) {
    const a = g.solution;
    const cells = [];
    const solutes = a.solutes.map((x) => {
      let t = chem(x.species);
      if (x.concentration) t += ' ' + esc(x.concentration) + ' mol/L';
      if (x.volumeLiters && x.volumeLiters !== '1') t += '，体积 ' + esc(x.volumeLiters) + ' L';
      return t;
    });
    if (solutes.length) cells.push('溶质：' + solutes.join('、'));
    if (a.ph) cells.push('题给 pH = ' + esc(a.ph));
    if (a.note) cells.push('附加条件：' + esc(a.note));
    if (a.orderSpecies.length) cells.push('待排序粒子：' + a.orderSpecies.map(chem).join('、'));
    if (a.conversion) {
      const cv = a.conversion;
      cells.push('沉淀转化：' + chem(cv.original) + ' + ' + chem(cv.addedIon) + ' → ' + chem(cv.target));
    }
    if (cells.length) rows.push(['溶液组成', `<ul class="given-steps">${cells.map((x) => `<li>${x}</li>`).join('')}</ul>`]);
  }

  if (!rows.length) return '';
  return `
  <details class="given">
    <summary>结构化已知条件（引擎输入）</summary>
    <dl class="given-grid">
      ${rows.map(([k, v]) => `<dt>${esc(k)}</dt><dd>${v}</dd>`).join('')}
    </dl>
  </details>`;
}

/* 解：状态 + 结构化答案 + 可溯源步骤 */
function solveHtml(s, category, given) {
  return `
    <p class="panel-note">引擎状态：${esc(s.message)}</p>
    ${answerHtml(s, category, given)}
    <h4>求解步骤（每步可溯源）</h4>
    <ol class="steps">
      ${s.steps.map(stepHtml).join('')}
    </ol>`;
}

/* 化学思想（五大）标签映射 */
const THOUGHT_META = {
  Conservation:  { label: '守恒思想',   icon: '⚖️', cls: 'th-conservation' },
  Structure:     { label: '结构决定性质', icon: '🔬', cls: 'th-structure' },
  Equilibrium:   { label: '动态平衡',   icon: '🔄', cls: 'th-equilibrium' },
  Classification:{ label: '分类比较',   icon: '🗂️', cls: 'th-classification' },
  ModelCognition:{ label: '证据推理·模型认知', icon: '🧩', cls: 'th-model' },
};

function stepHtml(st) {
  // 按 evidence 归类：空=引擎推理、RX-*/化学方程式=反应方程式、SOL-*/电解质=规则、其余=检索
  const ev = st.evidence || '';
  let stepType = 'lookup';
  let typeLabel = '检索';
  const isEquation = /[=→⇌≡↑↓]/.test(ev) && !/^given\./.test(ev) && !/ChemicalCheckerService/.test(ev);
  if (!ev) { stepType = 'reasoning'; typeLabel = '推理'; }
  else if (/^RX[-_]/.test(ev) || isEquation) { stepType = 'reaction'; typeLabel = '方程式'; }
  else if (/^SOL[-_]/.test(ev)) { stepType = 'rule'; typeLabel = '规则'; }
  else if (/electrolytes|hydrol|规则/i.test(ev)) { stepType = 'rule'; typeLabel = '规则'; }

  // 化学思想标签（后端已兜底推断，默认 ModelCognition）
  const thought = st.thought && THOUGHT_META[st.thought] ? THOUGHT_META[st.thought] : THOUGHT_META.ModelCognition;

  const chips = [];
  chips.push(`<span class="chip chip-type chip-type-${stepType}">${esc(typeLabel)}</span>`);
  chips.push(`<span class="chip chip-thought ${thought.cls}" title="本步使用的化学思想">${thought.icon} ${esc(thought.label)}</span>`);
  if (st.evidence) chips.push(`<span class="chip mono">${esc(st.evidence)}</span>`);
  if (st.knowledgeId) chips.push(`<span class="chip chip-k mono">${esc(st.knowledgeId)}</span>`);
  if (st.source) chips.push(`<span class="chip chip-src">${esc(st.source)}</span>`);
  return `
    <li class="step-timeline step-type-${stepType}">
      <div class="step-body">${chem(st.description)}</div>
      ${chips.length ? `<div class="chips">${chips.join('')}</div>` : ''}
    </li>`;
}

function answerHtml(s, category, given) {
  if (!s.isSolved) return `<p class="answer-none">${esc(s.message)}</p>`;

  switch (category) {
    case 'ProductInference':
      return eqBlock('产物方程式', s.equations) + atomBalanceHtml(s.equations);

    case 'IonCoexistence': {
      const v = COEXIST_TEXT[s.coexistence] || s.coexistence || '';
      const cls = s.coexistence === 'Coexist' ? 'v-ok' : 'v-no';
      const species = given?.species || [];
      const medium = given?.medium || '';
      return `<div class="answer-verdict ${cls}">${esc(v)}</div>` +
        coexistenceGraphHtml(s.conflicts, species, medium) +
        eqBlock('理由方程式', s.equations);
    }

    case 'SubstanceIdentification': {
      const groups = ['Must', 'MustNot', 'Maybe'].map((k) => {
        const list = Object.keys(s.composition).filter((x) => s.composition[x] === k);
        if (!list.length) return '';
        return `
          <div class="comp-row">
            <span class="comp-label comp-${k}">${COMPOSITION_LABEL[k]}</span>
            <span class="comp-list">${list.map(chem).join('、')}</span>
          </div>`;
      }).join('');
      return `<div class="answer-block">${groups}</div>` + eqBlock('证据方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'ProcessInference': {
      const map = Object.keys(s.substanceMap).sort().map((k) =>
        `<span class="map-item">${esc(k)} → ${chem(s.substanceMap[k])}</span>`).join('');
      return `
        <div class="answer-block">
          <div class="comp-row">
            <span class="comp-label comp-Must">物质对应</span>
            <span class="comp-list">${map}</span>
          </div>
        </div>` +
        processFlowHtml(given?.steps, s.substanceMap, null) +
        moleculeViewerHtml(s.molBlocks, s.substanceMap) +
        eqBlock('各步方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'StoichiometryCalc':
      return `
        <div class="answer-numeric">
          <b>${esc(s.numericValue)}</b><span>${esc(s.numericUnit)}</span>
          ${s.numericSpecies ? `<i>（${chem(s.numericSpecies)}）</i>` : ''}
        </div>` +
        moleRelationHtml(s.numericValue, s.numericUnit, s.numericSpecies) +
        eqBlock('反应方程式', s.equations) + atomBalanceHtml(s.equations);

    case 'Electrochemistry': {
      const el = s.electrodes || {};
      const cells = [['负极', el.negative], ['正极', el.positive],
        ['阳极', el.anode], ['阴极', el.cathode]]
        .filter(([, v]) => v)
        .map(([label, v]) => `
          <div class="electrode-cell">
            <h5>${esc(label)}</h5>
            <p class="mono">${chem(v)}</p>
          </div>`).join('');
      return `
        <div class="answer-block">
          <div class="electrode-grid">${cells}</div>
        </div>` +
        electrochemDiagramHtml(given?.cell, el) +
        eqBlock('总反应', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'ChemicalEquilibrium': {
      const q = s.equilibrium || {};
      const rows = (q.table || []).map((r) => {
        const change = r.change || '';
        const isNeg = change.startsWith('-');
        const changeCls = isNeg ? 'eq-change-neg' : (change !== '0' && change !== '' ? 'eq-change-pos' : '');
        return `
        <tr>
          <td>${chem(r.species)}</td>
          <td>${esc(r.start)}</td>
          <td class="${changeCls}">${esc(r.change)}</td>
          <td>${esc(r.end)}</td>
        </tr>`;
      }).join('');
      const concl = [];
      if (q.conversion) concl.push(`${chem(q.conversionSpecies)} 转化率 <b>${esc(q.conversion)}</b>`);
      if (q.fraction) concl.push(`${chem(q.fractionSpecies)} 摩尔分数 <b>${esc(q.fraction)}</b>`);
      if (q.kValue) concl.push(`Kp = <b>${esc(q.kValue)}</b>`);
      if (q.direction) concl.push(`扰动后平衡 <b class="eq-dir">${esc(DIRECTION_TEXT[q.direction] || q.direction)}</b>`);
      const arrowHtml = q.direction ? `<div class="eq-direction-arrow">${q.direction === 'Forward' ? '→ 正向移动' : q.direction === 'Reverse' ? '← 逆向移动' : '↔ 不移动'}</div>` : '';
      return `
        <div class="answer-block">
          <div class="eq-table-wrap">
            <table class="eq-table">
              <thead><tr><th>三段式（mol）</th><th>起始</th><th>转化</th><th>平衡</th></tr></thead>
              <tbody>${rows}</tbody>
            </table>
          </div>
          ${arrowHtml}
          ${concl.length ? `<div class="eq-concl">${concl.map((c) => `<span>${c}</span>`).join('')}</div>` : ''}
        </div>` +
        concentrationTimeHtml(q) +
        eqBlock('可逆反应', s.equations);
    }

    case 'AqueousIonEquilibrium': {
      const a = s.aqueous || {};
      const chain = (a.order || []).length
        ? `<div class="answer-block"><div class="order-chain">${a.order.map(chem).join('<span class="order-gt">&gt;</span>')}</div></div>`
        : '';
      const cons = (a.conservationEquations || []).length
        ? `
        <div class="eq-block">
          <h5>守恒关系</h5>
          <ul>${a.conservationEquations.map((c) => `<li>${chem(c)}</li>`).join('')}</ul>
        </div>`
        : '';
      return chain +
        concentrationBarsHtml(a.order) +
        cons +
        eqBlock('弱过程方程', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'IndustrialProcess': {
      const map = Object.keys(s.substanceMap || {}).sort().map((k) =>
        `<span class="map-item">${esc(k)} → ${chem(s.substanceMap[k])}</span>`).join('');
      const purposes = Object.keys(s.purposes || {}).sort().map((n) =>
        `
          <div class="comp-row">
            <span class="comp-label comp-Must">第 ${esc(n)} 步目的</span>
            <span class="comp-list purpose-text">${esc(s.purposes[n])}</span>
          </div>`).join('');
      return `
        <div class="answer-block">
          <div class="comp-row">
            <span class="comp-label comp-Must">物质对应</span>
            <span class="comp-list">${map}</span>
          </div>
          ${purposes}
        </div>` +
        processFlowHtml(given?.steps, s.substanceMap, s.purposes) +
        moleculeViewerHtml(s.molBlocks, s.substanceMap) +
        eqBlock('转化链方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'IonPowderIdentification': {
      const cols = ['Must', 'MustNot', 'Maybe'].map((k) => {
        const list = Object.keys(s.composition || {}).filter((x) => s.composition[x] === k);
        return `
          <div class="ionp-col">
            <h5 class="comp-label comp-${k}">${IONP_LABEL[k]}</h5>
            <div class="ionp-badges">
              ${list.length
                ? list.map((x) => `<span class="ionp-badge b-${k}">${chem(x)}</span>`).join('')
                : '<span class="ionp-none">—</span>'}
            </div>
          </div>`;
      }).join('');
      return `<div class="answer-block"><div class="ionp-grid">${cols}</div></div>` +
        eqBlock('证据方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'OrganicInference': {
      const keys = Object.keys(s.substanceMap || {}).sort((a, b) => a.localeCompare(b, 'en'));
      const chain = keys.length
        ? `<div class="org-chain">${keys.map((k) =>
            `<span class="org-node"><b>${esc(k)}</b><i>${chem(s.substanceMap[k])}</i></span>`
          ).join('<span class="org-arrow">→</span>')}</div>`
        : '';
      return `<div class="answer-block">${chain}</div>` +
        processFlowHtml(given?.steps, s.substanceMap, null) +
        moleculeViewerHtml(s.molBlocks, s.substanceMap) +
        eqBlock('各步方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    case 'ExperimentalInquiry': {
      const cons = (s.experiment && s.experiment.conclusions) || [];
      const items = cons.map((c) => {
        const i = c.indexOf('：');
        const label = i > 0 ? c.slice(0, i) : '实验结论';
        const body = i > 0 ? c.slice(i + 1) : c;
        return `
          <div class="exp-item">
            <h5>${chem(label)}</h5>
            <p>${chem(body)}</p>
          </div>`;
      }).join('');
      return `<div class="answer-block"><div class="exp-list">${items}</div></div>` +
        experimentApparatusHtml(given?.experiment, '') +
        eqBlock('原理方程式', s.equations) + atomBalanceHtml(s.equations);
    }

    default:
      return '';
  }
}

function eqBlock(title, equations) {
  if (!equations || !equations.length) return '';
  return `
    <div class="eq-block">
      <h5>${esc(title)}</h5>
      <ul>${equations.map((eq) => `<li>${chem(eq)}</li>`).join('')}</ul>
    </div>`;
}

/* ==================== 可视化函数 ==================== */

/* 化学式 → SVG tspan（SVG 中无法用 sub/sup，改用 baseline-shift 还原上下标） */
function chemSvg(s) {
  return chem(s)
    .replace(/<sup>/g, '<tspan baseline-shift="super" font-size="0.72em">')
    .replace(/<sub>/g, '<tspan baseline-shift="sub" font-size="0.72em">')
    .replace(/<\/sup>/g, '</tspan>')
    .replace(/<\/sub>/g, '</tspan>');
}

/* 估算文本宽度（中文按 1em，西文按 0.56em），用于自适应药丸/卡片宽度 */
function svgTextWidth(s, fontSize) {
  const plain = String(s).replace(/<[^>]+>/g, '');
  let w = 0;
  for (const ch of plain) {
    w += /[\u3000-\u303f\u4e00-\u9fa5\uff00-\uffef]/.test(ch) ? fontSize : fontSize * 0.56;
  }
  return w;
}

/* 通用 SVG 资源：玻璃/液体/金属渐变 + 柔和阴影 + 辉光 + 彩色箭头。prefix 避免同页多图 id 冲突 */
function svgCommonDefs(p) {
  const marker = (name, color) => `
    <marker id="${p}-${name}" markerWidth="9" markerHeight="9" refX="7.5" refY="4.5" orient="auto">
      <path d="M0,0 L9,4.5 L0,9 Z" fill="${color}"/>
    </marker>`;
  return `
  <defs>
    <!-- 卡片底：极浅的斜向光晕 -->
    <linearGradient id="${p}-panel" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#ffffff"/>
      <stop offset="0.55" stop-color="#fbfdff"/>
      <stop offset="1" stop-color="#f2f8fd"/>
    </linearGradient>
    <!-- 玻璃：对角渐变 + 冷色边缘 -->
    <linearGradient id="${p}-glass" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#ffffff"/>
      <stop offset="0.32" stop-color="#f6fbfe"/>
      <stop offset="0.6" stop-color="#e8f5fc"/>
      <stop offset="0.85" stop-color="#d8ecf8"/>
      <stop offset="1" stop-color="#c6e2f2"/>
    </linearGradient>
    <!-- 玻璃反光条（多条高光带） -->
    <linearGradient id="${p}-sheen" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#ffffff" stop-opacity="0"/>
      <stop offset="0.28" stop-color="#ffffff" stop-opacity="0.9"/>
      <stop offset="0.42" stop-color="#ffffff" stop-opacity="0"/>
      <stop offset="0.72" stop-color="#ffffff" stop-opacity="0.55"/>
      <stop offset="0.85" stop-color="#ffffff" stop-opacity="0"/>
      <stop offset="1" stop-color="#ffffff" stop-opacity="0.35"/>
    </linearGradient>
    <!-- 液体：上浅下深，模拟溶液厚度 -->
    <linearGradient id="${p}-liquid" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#a5e3fb" stop-opacity="0.5"/>
      <stop offset="0.35" stop-color="#4cc5f2" stop-opacity="0.62"/>
      <stop offset="1" stop-color="#0284c7" stop-opacity="0.82"/>
    </linearGradient>
    <!-- 液面反光 -->
    <radialGradient id="${p}-liquidHi" cx="0.32" cy="0.06" r="0.85">
      <stop offset="0" stop-color="#ffffff" stop-opacity="0.8"/>
      <stop offset="0.45" stop-color="#ffffff" stop-opacity="0.18"/>
      <stop offset="1" stop-color="#ffffff" stop-opacity="0"/>
    </radialGradient>
    <!-- 金属：多段明暗形成柱面高光 -->
    <linearGradient id="${p}-metal-warm" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#7c2d12"/>
      <stop offset="0.1" stop-color="#ea580c"/>
      <stop offset="0.3" stop-color="#fdba74"/>
      <stop offset="0.46" stop-color="#fff7ed"/>
      <stop offset="0.62" stop-color="#fb923c"/>
      <stop offset="0.86" stop-color="#c2410c"/>
      <stop offset="1" stop-color="#7c2d12"/>
    </linearGradient>
    <linearGradient id="${p}-metal-cool" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#475569"/>
      <stop offset="0.1" stop-color="#94a3b8"/>
      <stop offset="0.3" stop-color="#e2e8f0"/>
      <stop offset="0.46" stop-color="#ffffff"/>
      <stop offset="0.62" stop-color="#cbd5e1"/>
      <stop offset="0.86" stop-color="#64748b"/>
      <stop offset="1" stop-color="#475569"/>
    </linearGradient>
    <!-- 铁架台：金属杆竖向渐变 -->
    <linearGradient id="${p}-stand" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#64748b"/>
      <stop offset="0.35" stop-color="#cbd5e1"/>
      <stop offset="0.55" stop-color="#94a3b8"/>
      <stop offset="1" stop-color="#475569"/>
    </linearGradient>
    <!-- 火焰：外橙内白 -->
    <linearGradient id="${p}-flame" x1="0" y1="1" x2="0" y2="0">
      <stop offset="0" stop-color="#ea580c"/>
      <stop offset="0.4" stop-color="#f59e0b"/>
      <stop offset="0.72" stop-color="#fde047"/>
      <stop offset="1" stop-color="#fefce8"/>
    </linearGradient>
    <radialGradient id="${p}-flameGlow" cx="0.5" cy="0.72" r="0.62">
      <stop offset="0" stop-color="#fbbf24" stop-opacity="0.55"/>
      <stop offset="1" stop-color="#fbbf24" stop-opacity="0"/>
    </radialGradient>
    <!-- 柔和投影 -->
    <filter id="${p}-shadow" x="-30%" y="-30%" width="160%" height="160%">
      <feDropShadow dx="0" dy="3" stdDeviation="4" flood-color="#0f172a" flood-opacity="0.14"/>
    </filter>
    <!-- 内阴影：让玻璃器皿有厚度 -->
    <filter id="${p}-inner" x="-20%" y="-20%" width="140%" height="140%">
      <feGaussianBlur in="SourceAlpha" stdDeviation="4" result="b"/>
      <feOffset in="b" dy="3" result="o"/>
      <feComposite in="SourceAlpha" in2="o" operator="out" result="ring"/>
      <feFlood flood-color="#0c4a6e" flood-opacity="0.22" result="c"/>
      <feComposite in="c" in2="ring" operator="in" result="shade"/>
      <feMerge><feMergeNode in="SourceGraphic"/><feMergeNode in="shade"/></feMerge>
    </filter>
    <!-- 辉光 -->
    <filter id="${p}-glow" x="-60%" y="-60%" width="220%" height="220%">
      <feGaussianBlur stdDeviation="3" result="b"/>
      <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter>
    <!-- 细微颗粒纹理 -->
    <filter id="${p}-grain" x="0" y="0" width="100%" height="100%">
      <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="3" stitchTiles="stitch" result="n"/>
      <feColorMatrix in="n" type="saturate" values="0" result="g"/>
      <feComponentTransfer in="g"><feFuncA type="linear" slope="0.06"/></feComponentTransfer>
    </filter>
    ${marker('arr-blue', '#3b82f6')}
    ${marker('arr-red', '#dc2626')}
    ${marker('arr-amber', '#f59e0b')}
    ${marker('arr-green', '#10b981')}
    ${marker('arr-slate', '#64748b')}
  </defs>`;
}

/* 由离散点生成平滑三次贝塞尔路径（水平切线，适合指数型趋近曲线） */
function smoothLinePath(pts) {
  if (!pts.length) return '';
  let d = `M ${pts[0][0].toFixed(1)} ${pts[0][1].toFixed(1)}`;
  for (let i = 1; i < pts.length; i++) {
    const [x0, y0] = pts[i - 1];
    const [x1, y1] = pts[i];
    const dx = (x1 - x0) / 3;
    d += ` C ${(x0 + dx).toFixed(1)} ${y0.toFixed(1)}, ${(x1 - dx).toFixed(1)} ${y1.toFixed(1)}, ${x1.toFixed(1)} ${y1.toFixed(1)}`;
  }
  return d;
}

/* 1. 电化学装置图（SVG）：原电池/电解池，标注电极、电子流向、离子移向 */
function electrochemDiagramHtml(cell, electrodes) {
  if (!cell) return '';
  const galvanic = cell.type === 'galvanic';
  const oxLabel = galvanic ? '负极' : '阳极';
  const redLabel = galvanic ? '正极' : '阴极';
  const oxEq = galvanic ? electrodes?.negative : electrodes?.anode;
  const redEq = galvanic ? electrodes?.positive : electrodes?.cathode;
  const oxMat = (cell.electrodes?.[0] || '电极').slice(0, 8);
  const redMat = (cell.electrodes?.[1] || '电极').slice(0, 8);
  const electrolyte = cell.electrolyte || '电解质溶液';
  const medium = cell.medium === 'acidic' ? '酸性' : cell.medium === 'basic' ? '碱性' : '';
  const p = 'elec';

  // 半反应放在图下方（HTML 中可用上下标），避免长方程式在 SVG 内溢出
  const eqRow = (oxEq || redEq) ? `
    <div class="elec-eq-row">
      ${oxEq ? `<div class="elec-eq elec-eq-ox"><span class="elec-eq-tag">${oxLabel}·氧化</span><span class="elec-eq-formula">${chem(oxEq)}</span></div>` : ''}
      ${redEq ? `<div class="elec-eq elec-eq-red"><span class="elec-eq-tag">${redLabel}·还原</span><span class="elec-eq-formula">${chem(redEq)}</span></div>` : ''}
    </div>` : '';

  return `
  <div class="viz-block">
    <h5>🔋 装置示意图</h5>
    <div class="elec-diagram">
      <svg viewBox="0 0 640 300" class="elec-svg" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="电化学装置示意图">
        ${svgCommonDefs(p)}
        <defs>
          <clipPath id="${p}-beaker"><rect x="154" y="130" width="332" height="144" rx="13"/></clipPath>
        </defs>
        <rect x="1" y="1" width="638" height="298" rx="18" fill="url(#${p}-panel)" stroke="#e2e8f0"/>
        <rect x="1" y="1" width="638" height="298" rx="18" filter="url(#${p}-grain)" opacity="0.5"/>

        <!-- 外电路导线 -->
        <path d="M 200 96 L 200 56 Q 200 44 212 44 L 298 44" fill="none" stroke="#475569" stroke-width="3.5" stroke-linecap="round"/>
        <path d="M 342 44 L 428 44 Q 440 44 440 56 L 440 96" fill="none" stroke="#475569" stroke-width="3.5" stroke-linecap="round"/>
        ${galvanic ? `
          <circle cx="320" cy="44" r="21" fill="#ffffff" stroke="#475569" stroke-width="3"/>
          <text x="320" y="50" text-anchor="middle" font-size="15" font-weight="700" fill="#334155">A</text>
          <text x="320" y="17" text-anchor="middle" font-size="10.5" fill="#64748b">电流表</text>
        ` : `
          <rect x="290" y="28" width="60" height="32" rx="7" fill="#1f2937"/>
          <text x="320" y="49" text-anchor="middle" font-size="11.5" font-weight="700" fill="#fbbf24">直流电源</text>
          <text x="282" y="21" text-anchor="middle" font-size="13" font-weight="700" fill="#dc2626">＋</text>
          <text x="358" y="21" text-anchor="middle" font-size="13" font-weight="700" fill="#2563eb">－</text>
        `}

        <!-- 电子流动（动画虚线） -->
        <line class="elec-flow" x1="216" y1="44" x2="292" y2="44" stroke="#f59e0b" stroke-width="2.4" marker-end="url(#${p}-arr-amber)"/>
        <line class="elec-flow" x1="348" y1="44" x2="424" y2="44" stroke="#f59e0b" stroke-width="2.4" marker-end="url(#${p}-arr-amber)"/>
        <text x="320" y="76" text-anchor="middle" font-size="10.5" font-weight="600" fill="#b45309">e⁻ 外电路流向：${oxLabel} → ${redLabel}</text>

        <!-- 烧杯与电解质溶液 -->
        <rect x="150" y="126" width="340" height="152" rx="16" fill="url(#${p}-glass)" stroke="#94a3b8" stroke-width="2.5" filter="url(#${p}-inner)"/>
        <g clip-path="url(#${p}-beaker)">
          <rect x="154" y="172" width="332" height="102" fill="url(#${p}-liquid)"/>
          <ellipse cx="320" cy="172" rx="166" ry="5" fill="#e0f2fe" opacity="0.65"/>
          <rect x="154" y="172" width="332" height="102" fill="url(#${p}-liquidHi)"/>
          <path d="M 176 246 q 40 -6 80 0 t 80 0" fill="none" stroke="#e0f2fe" stroke-width="1.4" opacity="0.5"/>
          <path d="M 300 258 q 40 -6 80 0" fill="none" stroke="#e0f2fe" stroke-width="1.2" opacity="0.35"/>
        </g>
        <rect x="160" y="134" width="7" height="128" rx="3.5" fill="url(#${p}-sheen)" opacity="0.85"/>
        <rect x="462" y="140" width="5" height="112" rx="2.5" fill="#ffffff" opacity="0.4"/>
        <text x="320" y="204" text-anchor="middle" font-size="13" font-weight="600" fill="#0c4a6e">${esc(electrolyte)}${medium ? '（' + medium + '）' : ''}</text>

        <!-- 电极 -->
        <rect x="186" y="96" width="28" height="164" rx="9" fill="url(#${p}-metal-cool)" stroke="#475569" stroke-width="1.2" filter="url(#${p}-shadow)"/>
        <rect x="426" y="96" width="28" height="164" rx="9" fill="url(#${p}-metal-warm)" stroke="#9a3412" stroke-width="1.2" filter="url(#${p}-shadow)"/>
        <rect x="190" y="102" width="5" height="150" rx="2.5" fill="#ffffff" opacity="0.55"/>
        <rect x="430" y="102" width="5" height="150" rx="2.5" fill="#ffffff" opacity="0.6"/>
        <text x="200" y="178" text-anchor="middle" dominant-baseline="central" font-size="11.5" font-weight="700" fill="#1e293b" transform="rotate(-90 200 178)">${esc(oxMat)}</text>
        <text x="440" y="178" text-anchor="middle" dominant-baseline="central" font-size="11.5" font-weight="700" fill="#7c2d12" transform="rotate(-90 440 178)">${esc(redMat)}</text>

        <!-- 离子移向（内电路） -->
        <text x="248" y="228" text-anchor="middle" font-size="11" fill="#1d4ed8">阳离子</text>
        <line x1="214" y1="242" x2="288" y2="242" stroke="#3b82f6" stroke-width="2.4" marker-end="url(#${p}-arr-blue)"/>
        <text x="392" y="228" text-anchor="middle" font-size="11" fill="#b91c1c">阴离子</text>
        <line x1="426" y1="242" x2="352" y2="242" stroke="#dc2626" stroke-width="2.4" marker-end="url(#${p}-arr-red)"/>

        <!-- 电极角色标注（虚线引出） -->
        <line x1="128" y1="163" x2="184" y2="163" stroke="#cbd5e1" stroke-width="1.5" stroke-dasharray="4 3"/>
        <line x1="512" y1="163" x2="456" y2="163" stroke="#cbd5e1" stroke-width="1.5" stroke-dasharray="4 3"/>
        <g filter="url(#${p}-shadow)">
          <rect x="16" y="144" width="112" height="38" rx="11" fill="#fff7ed" stroke="#fb923c" stroke-width="1.6"/>
          <text x="72" y="160" text-anchor="middle" font-size="12.5" font-weight="700" fill="#9a3412">${oxLabel}</text>
          <text x="72" y="174" text-anchor="middle" font-size="9.5" fill="#b45309">氧化 · 失电子</text>
        </g>
        <g filter="url(#${p}-shadow)">
          <rect x="512" y="144" width="112" height="38" rx="11" fill="#eff6ff" stroke="#60a5fa" stroke-width="1.6"/>
          <text x="568" y="160" text-anchor="middle" font-size="12.5" font-weight="700" fill="#1e40af">${redLabel}</text>
          <text x="568" y="174" text-anchor="middle" font-size="9.5" fill="#2563eb">还原 · 得电子</text>
        </g>
      </svg>
      ${eqRow}
      <p class="viz-caption">${galvanic ? '原电池：负极氧化失电子，正极还原得电子；电子经外电路由负极流向正极，阳离子向正极移动、阴离子向负极移动。' : '电解池：阳极（接电源正极）氧化，阴极（接电源负极）还原；电子由阳极经外电路流向阴极。'}</p>
    </div>
  </div>`;
}

/* 2. 转化链流程图 */
function processFlowHtml(steps, substanceMap, purposes) {
  if (!steps || !steps.length) return '';
  const nodes = [];
  const edges = [];
  steps.forEach((st) => {
    if (st.from && !nodes.includes(st.from)) nodes.push(st.from);
    if (st.to && !nodes.includes(st.to)) nodes.push(st.to);
    if (st.from && st.to) {
      edges.push({ from: st.from, to: st.to, step: st });
    }
  });
  if (!nodes.length) return '';

  const nodeHtml = nodes.map((n) => {
    const substance = substanceMap?.[n];
    return `
      <div class="flow-node">
        <div class="flow-node-label">${esc(n)}</div>
        ${substance ? `<div class="flow-node-subst">${chem(substance)}</div>` : '<div class="flow-node-pending">待推断</div>'}
      </div>`;
  }).join('<div class="flow-arrow">→</div>');

  const edgeNotes = edges.map((e) => {
    const parts = [];
    if (e.step.reagents?.length) parts.push('加 ' + e.step.reagents.map(chem).join('、'));
    if (e.step.conditions?.length) parts.push(e.step.conditions.map(esc).join('、'));
    if (e.step.phenomenon) parts.push(esc(e.step.phenomenon));
    return parts.length ? `<div class="flow-edge-note">${parts.join('｜')}</div>` : '';
  }).join('');

  const purposeHtml = purposes && Object.keys(purposes).length ? `
    <div class="flow-purposes">
      ${Object.keys(purposes).sort().map((k) => `
        <div class="flow-purpose"><b>第 ${esc(k)} 步：</b>${esc(purposes[k])}</div>
      `).join('')}
    </div>` : '';

  return `
  <div class="viz-block">
    <h5>🔗 转化链流程图</h5>
    <div class="flow-chart">${nodeHtml}</div>
    ${edgeNotes}
    ${purposeHtml}
  </div>`;
}

/* 3. 粒子浓度定性柱状图（SVG：渐变柱 + 排序标记） */
function concentrationBarsHtml(order) {
  if (!order || !order.length) return '';
  const n = order.length;
  const barW = 46;
  const gap = 30;
  const plotW = n * barW + (n - 1) * gap;
  const W = Math.max(380, plotW + 120);
  const H = 268;
  const baseY = 208;
  const topY = 44;
  const usable = baseY - topY;
  const x0 = (W - plotW) / 2;

  const grid = [0, 0.25, 0.5, 0.75, 1].map((f) => {
    const y = baseY - usable * f;
    return `<line x1="${(x0 - 18).toFixed(1)}" y1="${y.toFixed(1)}" x2="${(x0 + plotW + 18).toFixed(1)}" y2="${y.toFixed(1)}"
      stroke="#eef2f7" stroke-width="1" ${f === 0 ? 'stroke="#cbd5e1" stroke-width="1.6"' : ''}/>`;
  }).join('');

  const bars = order.map((sp, i) => {
    const h = Math.max(18, usable * 0.95 * (1 - i / (n + 0.4)));
    const x = x0 + i * (barW + gap);
    const y = baseY - h;
    return `
      <g class="bar-g">
        <rect x="${x.toFixed(1)}" y="${(y + 8).toFixed(1)}" width="${barW}" height="${h.toFixed(1)}" rx="9"
          fill="#6366f1" opacity="${(0.16 - i * 0.015).toFixed(3)}"/>
        <rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barW}" height="${h.toFixed(1)}" rx="9"
          fill="url(#bar-grad)" opacity="${(1 - i * 0.09).toFixed(2)}"/>
        <rect x="${(x + 3).toFixed(1)}" y="${(y + 3).toFixed(1)}" width="${(barW - 6)}" height="${Math.min(h * 0.45, 42).toFixed(1)}" rx="7"
          fill="url(#bar-gloss)" opacity="0.75"/>
        <rect x="${(x + barW - 11).toFixed(1)}" y="${(y + 8).toFixed(1)}" width="5" height="${Math.max(h - 20, 10).toFixed(1)}" rx="2.5"
          fill="#ffffff" opacity="0.22"/>
        <circle cx="${(x + barW / 2).toFixed(1)}" cy="${(y - 12).toFixed(1)}" r="9" fill="#eef2ff" stroke="#c7d2fe"/>
        <text x="${(x + barW / 2).toFixed(1)}" y="${(y - 8.5).toFixed(1)}" text-anchor="middle" font-size="10" font-weight="700" fill="#4f46e5">${i + 1}</text>
        <text x="${(x + barW / 2).toFixed(1)}" y="${baseY + 22}" text-anchor="middle" font-size="10.5" fill="#334155">${chemSvg(sp)}</text>
      </g>`;
  }).join('');

  return `
  <div class="viz-block">
    <h5>📊 粒子浓度相对大小</h5>
    <div class="bar-chart">
      <svg viewBox="0 0 ${W} ${H}" class="bar-svg" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="粒子浓度相对大小柱状图">
        <defs>
          <linearGradient id="bar-grad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stop-color="#a5b4fc"/>
            <stop offset="0.55" stop-color="#818cf8"/>
            <stop offset="1" stop-color="#6366f1"/>
          </linearGradient>
          <linearGradient id="bar-gloss" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stop-color="#ffffff" stop-opacity="0.55"/>
            <stop offset="1" stop-color="#ffffff" stop-opacity="0"/>
          </linearGradient>
          <filter id="bar-shadow" x="-30%" y="-30%" width="160%" height="160%">
            <feDropShadow dx="0" dy="3" stdDeviation="3" flood-color="#4338ca" flood-opacity="0.18"/>
          </filter>
          <marker id="bar-axis" markerWidth="8" markerHeight="8" refX="4" refY="6" orient="auto">
            <path d="M0,8 L4,0 L8,8 Z" fill="#cbd5e1"/>
          </marker>
        </defs>
        ${grid}
        <g filter="url(#bar-shadow)">${bars}</g>
        <text x="16" y="22" font-size="10.5" fill="#94a3b8">相对浓度（由大到小）</text>
        <line x1="${(x0 - 18).toFixed(1)}" y1="${baseY}" x2="${(x0 - 18).toFixed(1)}" y2="${(topY - 6).toFixed(1)}"
          stroke="#cbd5e1" stroke-width="1.6" marker-end="url(#bar-axis)"/>
      </svg>
    </div>
    <p class="viz-caption">注：柱高为相对示意（按浓度由大到小排序），不代表真实浓度比。</p>
  </div>`;
}

/* 4. 离子共存冲突图（SVG 环形网络：节点环绕、冲突弧线着色） */
function coexistenceGraphHtml(conflicts, species, medium) {
  if (!species || !species.length) return '';
  const allIons = [...species];
  if (medium === 'acidic' && !allIons.includes('H^+')) allIons.push('H^+');
  if (medium === 'basic' && !allIons.includes('OH^-')) allIons.push('OH^-');

  const reasonColor = {
    '生成沉淀': '#dc2626',
    '生成气体': '#ea580c',
    '发生氧化还原反应': '#7c3aed',
    '相互促进水解（双水解）': '#0891b2',
    '生成弱电解质': '#ca8a04',
  };
  const legendItems = [
    ['生成沉淀', '#dc2626'],
    ['生成气体', '#ea580c'],
    ['氧化还原', '#7c3aed'],
    ['双水解', '#0891b2'],
    ['弱电解质', '#ca8a04'],
  ];

  // 冲突对 → 原因（同一对只保留首个原因）
  const pairMap = new Map();
  (conflicts || []).forEach((c) => {
    const ions = c.ions || [];
    for (let i = 0; i < ions.length; i++) {
      for (let j = i + 1; j < ions.length; j++) {
        const key = [ions[i], ions[j]].sort().join('|');
        if (!pairMap.has(key)) pairMap.set(key, { a: ions[i], b: ions[j], reason: c.reason || '' });
      }
    }
  });

  const pairList = Array.from(pairMap.values());
  const hasConflict = (ion) => pairList.some((pr) => pr.a === ion || pr.b === ion);
  const reasonsOf = (ion) => Array.from(new Set(pairList.filter((pr) => pr.a === ion || pr.b === ion).map((pr) => pr.reason).filter(Boolean)));

  // 环形（椭圆）布局
  const n = allIons.length;
  const W = 560;
  const H = Math.max(320, 168 + n * 28);
  const cx = W / 2;
  const cy = H / 2;
  const rx = W / 2 - 96;
  const ry = H / 2 - 64;
  const pos = {};
  allIons.forEach((ion, i) => {
    const ang = -Math.PI / 2 + (i * 2 * Math.PI) / n;
    pos[ion] = { x: cx + rx * Math.cos(ang), y: cy + ry * Math.sin(ang) };
  });

  const edges = pairList.map((pr) => {
    const A = pos[pr.a], B = pos[pr.b];
    if (!A || !B) return '';
    const mx = (A.x + B.x) / 2, my = (A.y + B.y) / 2;
    const qx = cx + (mx - cx) * 0.42;
    const qy = cy + (my - cy) * 0.42;
    const color = reasonColor[pr.reason] || '#94a3b8';
    return `<path d="M ${A.x.toFixed(1)} ${A.y.toFixed(1)} Q ${qx.toFixed(1)} ${qy.toFixed(1)} ${B.x.toFixed(1)} ${B.y.toFixed(1)}"
      fill="none" stroke="${color}" stroke-width="2.2" stroke-linecap="round" opacity="0.5">
      <title>${esc(pr.a)} 与 ${esc(pr.b)}：${esc(pr.reason || '不能大量共存')}</title>
    </path>`;
  }).join('');

  const nodes = allIons.map((ion) => {
    const { x, y } = pos[ion];
    const bad = hasConflict(ion);
    const w = Math.max(54, svgTextWidth(ion, 12) + 26);
    const h = 32;
    return `
      <g class="coex-node-g">
        <title>${esc(reasonsOf(ion).join('；') || '无冲突')}</title>
        <rect x="${(x - w / 2).toFixed(1)}" y="${(y - h / 2).toFixed(1)}" width="${w.toFixed(1)}" height="${h}" rx="${h / 2}"
          fill="${bad ? '#fef2f2' : '#f0fdf4'}" stroke="${bad ? '#dc2626' : '#16a34a'}" stroke-width="2" filter="url(#coex-shadow)"/>
        <rect x="${(x - w / 2 + 6).toFixed(1)}" y="${(y - h / 2 + 3).toFixed(1)}" width="${Math.max(w - 22, 20).toFixed(1)}" height="6" rx="3"
          fill="#ffffff" opacity="0.6"/>
        <text x="${x.toFixed(1)}" y="${(y + 4.4).toFixed(1)}" text-anchor="middle" font-size="12" font-weight="700"
          fill="${bad ? '#b91c1c' : '#15803d'}">${chemSvg(ion)}</text>
      </g>`;
  }).join('');

  const verdict = pairList.length
    ? `<rect x="${cx - 80}" y="${cy - 21}" width="160" height="42" rx="21" fill="#fff1f2" stroke="#fda4af"/>
       <text x="${cx}" y="${cy + 5}" text-anchor="middle" font-size="13" font-weight="700" fill="#be123c">⚠ 不能大量共存</text>`
    : `<rect x="${cx - 80}" y="${cy - 21}" width="160" height="42" rx="21" fill="#f0fdf4" stroke="#86efac"/>
       <text x="${cx}" y="${cy + 5}" text-anchor="middle" font-size="13" font-weight="700" fill="#15803d">✓ 能大量共存</text>`;

  const legend = pairList.length ? `
    <div class="coex-legend">
      ${legendItems.map(([label, color]) => `
        <span><span class="legend-dot" style="background:${color}"></span>${label}</span>`).join('')}
    </div>` : '';

  return `
  <div class="viz-block">
    <h5>🔗 离子共存冲突图</h5>
    <div class="coex-graph">
      <svg viewBox="0 0 ${W} ${H}" class="coex-svg" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="离子共存冲突关系图">
        <defs>
          <linearGradient id="coex-panel" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stop-color="#ffffff"/>
            <stop offset="1" stop-color="#f4f9fe"/>
          </linearGradient>
          <radialGradient id="coex-glow" cx="0.5" cy="0.5" r="0.62">
            <stop offset="0" stop-color="#eef2ff" stop-opacity="0.9"/>
            <stop offset="1" stop-color="#eef2ff" stop-opacity="0"/>
          </radialGradient>
          <filter id="coex-shadow" x="-40%" y="-40%" width="180%" height="180%">
            <feDropShadow dx="0" dy="2" stdDeviation="3" flood-color="#0f172a" flood-opacity="0.16"/>
          </filter>
        </defs>
        <rect x="1" y="1" width="${W - 2}" height="${H - 2}" rx="16" fill="url(#coex-panel)" stroke="#e2e8f0"/>
        <ellipse cx="${cx}" cy="${cy}" rx="${rx + 40}" ry="${ry + 30}" fill="url(#coex-glow)"/>
        ${edges}
        ${nodes}
        ${verdict}
      </svg>
      ${legend}
    </div>
  </div>`;
}

/* 5. 摩尔计算关系图（SVG 放射状：中心 n 与四向换算卡片） */
function moleRelationHtml(numericValue, numericUnit, numericSpecies) {
  const u = numericUnit || '';
  const highlight = {
    mass: u === 'g',
    mole: u === 'mol',
    gas: u === 'L',
    conc: u === 'mol/L' || u === 'mol·L⁻¹',
  };

  const W = 560;
  const H = 330;
  const cx = W / 2;
  const cy = H / 2;

  const cards = [
    { key: 'mass', label: '质量 m', formula: 'm = n · M', x: 30, y: 26, anchor: [cx - 18, cy - 46] },
    { key: 'gas', label: '气体体积 V', formula: 'V = n · Vₘ', x: 380, y: 26, anchor: [cx + 18, cy - 46] },
    { key: 'mole', label: '物质的量 n', formula: 'n = m / M', x: 30, y: 248, anchor: [cx - 18, cy + 46] },
    { key: 'conc', label: '浓度 c', formula: 'c = n / V', x: 380, y: 248, anchor: [cx + 18, cy + 46] },
  ];

  const cardHtml = cards.map((c) => {
    const hl = highlight[c.key];
    const cw = 150;
    const ch = 56;
    const [ax, ay] = c.anchor;
    const startX = c.x + (c.x < cx ? cw : 0);
    const startY = c.y + ch / 2;
    return `
      <g>
        <line x1="${startX}" y1="${startY}" x2="${ax}" y2="${ay}"
          stroke="${hl ? '#6366f1' : '#cbd5e1'}" stroke-width="${hl ? 2.2 : 1.6}"
          stroke-dasharray="5 4" marker-end="url(#mole-arr)"/>
        <rect x="${c.x}" y="${c.y}" width="${cw}" height="${ch}" rx="12"
          fill="${hl ? '#eef2ff' : '#ffffff'}" stroke="${hl ? '#6366f1' : '#e2e8f0'}" stroke-width="${hl ? 2.2 : 1.5}"/>
        <text x="${c.x + cw / 2}" y="${c.y + 22}" text-anchor="middle" font-size="12" font-weight="600" fill="#334155">${esc(c.label)}</text>
        <text x="${c.x + cw / 2}" y="${c.y + 42}" text-anchor="middle" font-size="11.5" fill="${hl ? '#4338ca' : '#64748b'}">${esc(c.formula)}</text>
      </g>`;
  }).join('');

  const centerValue = numericValue ? `${esc(numericValue)} ${esc(numericUnit || 'mol')}` : 'n = ?';

  return `
  <div class="viz-block">
    <h5>🧮 物质的量换算关系</h5>
    <div class="mole-diagram">
      <svg viewBox="0 0 ${W} ${H}" class="mole-svg" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="物质的量换算关系图">
        <defs>
          <linearGradient id="mole-center-grad" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stop-color="#818cf8"/>
            <stop offset="1" stop-color="#4f46e5"/>
          </linearGradient>
          <radialGradient id="mole-hi" cx="0.5" cy="0.3" r="0.7">
            <stop offset="0" stop-color="#ffffff" stop-opacity="0.85"/>
            <stop offset="1" stop-color="#ffffff" stop-opacity="0"/>
          </radialGradient>
          <marker id="mole-arr" markerWidth="8" markerHeight="8" refX="4" refY="6" orient="auto">
            <path d="M0,8 L4,0 L8,8 Z" fill="#94a3b8"/>
          </marker>
          <filter id="mole-shadow" x="-40%" y="-40%" width="180%" height="180%">
            <feDropShadow dx="0" dy="4" stdDeviation="5" flood-color="#4338ca" flood-opacity="0.3"/>
          </filter>
        </defs>
        ${cardHtml}
        <circle cx="${cx}" cy="${cy}" r="54" fill="url(#mole-center-grad)" filter="url(#mole-shadow)"/>
        <ellipse cx="${cx - 14}" cy="${cy - 24}" rx="30" ry="16" fill="url(#mole-hi)" opacity="0.55"/>
        <circle cx="${cx}" cy="${cy}" r="54" fill="none" stroke="#ffffff" stroke-width="1.4" opacity="0.35"/>
        <text x="${cx}" y="${cy - 10}" text-anchor="middle" font-size="11.5" font-weight="600" fill="#e0e7ff">物质的量 n</text>
        <text x="${cx}" y="${cy + 14}" text-anchor="middle" font-size="17" font-weight="700" fill="#ffffff">${centerValue}</text>
      </svg>
      ${numericSpecies ? `<p class="viz-caption">目标物种：${chem(numericSpecies)}</p>` : ''}
    </div>
  </div>`;
}

/* 6. 方程式配平·原子守恒校验 */
function atomBalanceHtml(equations) {
  if (!equations || !equations.length) return '';
  const blocks = equations.map((eq) => {
    // 简化解析：提取左右两侧各元素原子数（处理下标、系数、括号）
    const sides = eq.split(/→|⇌|=/).map((s) => s.trim());
    if (sides.length < 2) return '';
    const left = countAtoms(sides[0]);
    const right = countAtoms(sides[1]);
    const allEls = new Set([...Object.keys(left), ...Object.keys(right)]);
    const balanced = Array.from(allEls).every((el) => left[el] === right[el]);
    const rows = Array.from(allEls).map((el) => {
      const l = left[el] || 0, r = right[el] || 0;
      const ok = l === r;
      return `<tr class="${ok ? 'ab-ok' : 'ab-fail'}">
        <td>${esc(el)}</td><td>${l}</td><td>${r}</td>
        <td>${ok ? '✓' : '✗'}</td></tr>`;
    }).join('');
    return `
      <div class="atom-balance">
        <p class="ab-eq">${chem(eq)}</p>
        <table class="ab-table">
          <thead><tr><th>元素</th><th>左侧</th><th>右侧</th><th>守恒</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>
        <p class="ab-verdict ${balanced ? 'ab-ok-text' : 'ab-fail-text'}">${balanced ? '✓ 原子守恒，配平正确' : '✗ 原子不守恒'}</p>
      </div>`;
  }).filter(Boolean).join('');

  if (!blocks) return '';
  return `
  <div class="viz-block">
    <h5>⚖️ 原子守恒校验</h5>
    ${blocks}
  </div>`;
}

/* 辅助：统计一侧的各元素原子数（简易解析，支持系数、下标、括号） */
function countAtoms(side) {
  const counts = {};
  // 去掉电荷上标和状态符号
  const s = side.replace(/\^[^\s+]+/g, '').replace(/[（(].*?[)）]/g, '');
  const tokens = s.match(/[A-Z][a-z]?\d*|\d+(?=[A-Z(（])/g) || [];
  let i = 0;
  while (i < tokens.length) {
    const t = tokens[i];
    if (/^[A-Z]/.test(t)) {
      const el = t.match(/[A-Z][a-z]?/)[0];
      const num = parseInt(t.match(/\d+/)?.[0] || '1', 10);
      counts[el] = (counts[el] || 0) + num;
    }
    i++;
  }
  return counts;
}

/* 辅助：解析化学量字符串（支持分数、小数、整数） */
function parseChemVal(s) {
  if (!s) return 0;
  const str = String(s).trim();
  if (str.includes('/')) {
    const [a, b] = str.split('/').map(Number);
    return b ? a / b : 0;
  }
  return parseFloat(str) || 0;
}

/* 7. 浓度-时间图（化学平衡） */
function concentrationTimeHtml(equilibrium) {
  const table = equilibrium?.table;
  if (!table || !table.length) return '';

  // 解析每个物种的起始和平衡浓度
  const series = table.map((r) => ({
    species: r.species,
    start: parseChemVal(r.start),
    end: parseChemVal(r.end),
  })).filter((s) => s.start !== 0 || s.end !== 0);

  if (!series.length) return '';

  const W = 560, H = 300;
  const padL = 58, padR = 26, padT = 26, padB = 46;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;

  // 找最大值作为 y 轴上限
  const maxVal = Math.max(...series.flatMap((s) => [s.start, s.end]), 0.1);
  const yMax = Math.ceil(maxVal * 1.1 * 10) / 10;

  // 时间轴：0 → t_eq（达到平衡）；若有扰动，在 60% 处标记
  const hasDisturbance = !!equilibrium?.direction;
  const tEq = 100;
  const tDisturb = hasDisturbance ? 60 : -1;

  // 生成平滑曲线（指数趋近平衡），导出三次贝塞尔路径
  const palette = ['#6366f1', '#f43f5e', '#10b981', '#f59e0b', '#8b5cf6', '#06b6d4'];
  const pts = series.map((s, idx) => {
    const color = palette[idx % palette.length];
    const raw = [];
    for (let t = 0; t <= tEq; t += 4) {
      const frac = t / tEq;
      // 指数趋近：c(t) = end + (start - end) * exp(-k*t)，k 使 t=tEq 时接近 99%
      const k = -Math.log(0.01) / tEq;
      const c = s.end + (s.start - s.end) * Math.exp(-k * t);
      raw.push([padL + frac * plotW, padT + plotH - (c / yMax) * plotH]);
    }
    return {
      species: s.species,
      color,
      d: smoothLinePath(raw),
      startDot: raw[0],
      endDot: raw[raw.length - 1],
    };
  });

  // 网格与刻度
  const yTicks = 5;
  const yTickLines = Array.from({ length: yTicks + 1 }, (_, i) => {
    const v = (yMax / yTicks) * i;
    const y = padT + plotH - (v / yMax) * plotH;
    return `<line x1="${padL}" y1="${y.toFixed(1)}" x2="${padL + plotW}" y2="${y.toFixed(1)}" stroke="#eef2f7" stroke-width="1"/>
            <text x="${padL - 9}" y="${(y + 4).toFixed(1)}" text-anchor="end" font-size="10" fill="#94a3b8">${v.toFixed(1)}</text>`;
  }).join('');

  const xLabels = `<text x="${padL}" y="${H - padB + 20}" text-anchor="middle" font-size="10.5" fill="#94a3b8">0</text>
    <text x="${padL + plotW}" y="${H - padB + 20}" text-anchor="middle" font-size="11" fill="#64748b">t<tspan baseline-shift="sub" font-size="9">eq</tspan></text>
    <text x="${padL + plotW / 2}" y="${H - 8}" text-anchor="middle" font-size="12" fill="#475569">时间 t</text>
    <text x="16" y="${padT + plotH / 2}" text-anchor="middle" font-size="12" fill="#475569" transform="rotate(-90 16 ${padT + plotH / 2})">浓度 c</text>`;

  const disturbLine = hasDisturbance ? `
    <line x1="${(padL + (tDisturb / tEq) * plotW).toFixed(1)}" y1="${padT}" x2="${(padL + (tDisturb / tEq) * plotW).toFixed(1)}" y2="${padT + plotH}" stroke="#f43f5e" stroke-width="1.6" stroke-dasharray="5 4"/>
    <rect x="${(padL + (tDisturb / tEq) * plotW - 20).toFixed(1)}" y="${padT - 22}" width="40" height="17" rx="8" fill="#fff1f2" stroke="#fda4af"/>
    <text x="${(padL + (tDisturb / tEq) * plotW).toFixed(1)}" y="${padT - 10}" text-anchor="middle" font-size="9.5" font-weight="600" fill="#e11d48">扰动</text>
  ` : '';

  const legend = pts.map((p) => `
    <span class="ct-legend-item">
      <span class="ct-legend-dot" style="background:${p.color}"></span>${chem(p.species)}
    </span>
  `).join('');

  const paths = pts.map((p) => `
    <path d="${p.d}" fill="none" stroke="${p.color}" stroke-width="6" stroke-linecap="round" opacity="0.16" filter="url(#ct-soft)"/>
    <path class="ct-line" d="${p.d}" fill="none" stroke="${p.color}" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" pathLength="1"/>
    <circle cx="${p.startDot[0].toFixed(1)}" cy="${p.startDot[1].toFixed(1)}" r="3.2" fill="#fff" stroke="${p.color}" stroke-width="2"/>
    <circle cx="${p.endDot[0].toFixed(1)}" cy="${p.endDot[1].toFixed(1)}" r="3.6" fill="${p.color}"/>
  `).join('');

  return `
  <div class="viz-block">
    <h5>📈 浓度-时间示意图</h5>
    <div class="ct-chart">
      <svg viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="浓度随时间变化示意图">
        <defs>
          <linearGradient id="ct-panel" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stop-color="#ffffff"/>
            <stop offset="1" stop-color="#f7fbff"/>
          </linearGradient>
          <filter id="ct-soft" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation="4"/>
          </filter>
          <marker id="ct-arr" markerWidth="9" markerHeight="9" refX="7" refY="4.5" orient="auto">
            <path d="M0,0 L9,4.5 L0,9 Z" fill="#94a3b8"/>
          </marker>
        </defs>
        <rect x="0" y="0" width="${W}" height="${H}" rx="12" fill="url(#ct-panel)"/>
        ${yTickLines}
        <line x1="${padL}" y1="${padT + plotH}" x2="${padL}" y2="${padT - 6}" stroke="#cbd5e1" stroke-width="1.6" marker-end="url(#ct-arr)"/>
        <line x1="${padL}" y1="${padT + plotH}" x2="${padL + plotW + 10}" y2="${padT + plotH}" stroke="#cbd5e1" stroke-width="1.6" marker-end="url(#ct-arr)"/>
        ${disturbLine}
        ${paths}
        ${xLabels}
      </svg>
      <div class="ct-legend">${legend}</div>
    </div>
    <p class="viz-caption">注：曲线为指数趋近平衡的示意，非真实动力学数据；${hasDisturbance ? '红色虚线为施加扰动的时刻。' : '浓度从起始值逐渐趋近平衡值。'}</p>
  </div>`;
}

/* 8. 实验装置图 */
function experimentApparatusHtml(experiment, problemText) {
  const ops = experiment?.operations || [];
  const text = (problemText || '') + (ops.join(' '));

  // 根据关键词判断需要展示的装置
  const apparatus = [];
  if (/蒸馏|distillation/i.test(text)) apparatus.push('distillation');
  if (/萃取|分液|extraction/i.test(text)) apparatus.push('extraction');
  if (/过滤|filtration|漏斗/.test(text)) apparatus.push('filtration');
  if (/蒸发|结晶|evaporation|crystalliz/i.test(text)) apparatus.push('evaporation');
  if (/滴定|buret|滴定管|conical/i.test(text)) apparatus.push('titration');
  if (/气密性|airtightness|微热/.test(text)) apparatus.push('airtightness');
  if (/气体|gas|集气|制气/.test(text) && apparatus.length === 0) apparatus.push('gas');

  if (!apparatus.length) return '';

  const svgs = apparatus.map((type) => APPARATUS_SVG[type]).filter(Boolean).join('');
  if (!svgs) return '';

  return `
  <div class="viz-block">
    <h5>🔬 实验装置示意图</h5>
    <div class="apparatus-grid">${svgs}</div>
  </div>`;
}

/* 实验装置 SVG 库 */
const APPARATUS_SVG = {
  distillation: `
    <div class="apparatus-item">
      <svg viewBox="0 0 360 220" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="蒸馏装置示意图">
        ${svgCommonDefs('apd')}
        <defs><clipPath id="apd-body"><circle cx="150" cy="132" r="37"/></clipPath></defs>
        <!-- 铁架台 -->
        <rect x="56" y="16" width="5" height="188" rx="2.5" fill="url(#apd-stand)"/>
        <rect x="24" y="198" width="70" height="7" rx="3.5" fill="url(#apd-stand)"/>
        <rect x="61" y="158" width="54" height="5" rx="2.5" fill="url(#apd-stand)"/>
        <!-- 蒸馏烧瓶 -->
        <g filter="url(#apd-shadow)">
          <path d="M 139 64 L 139 96 L 134 96 A 40 40 0 1 0 166 96 L 161 96 L 161 64 Z"
                fill="url(#apd-glass)" stroke="#60a5fa" stroke-width="2.2" stroke-linejoin="round"/>
        </g>
        <rect x="112" y="146" width="76" height="30" fill="url(#apd-liquid)" clip-path="url(#apd-body)" opacity="0.85"/>
        <rect x="112" y="146" width="76" height="30" fill="url(#apd-liquidHi)" clip-path="url(#apd-body)" opacity="0.8"/>
        <rect x="122" y="102" width="7" height="66" rx="3.5" fill="url(#apd-sheen)" clip-path="url(#apd-body)" opacity="0.9"/>
        <rect x="164" y="120" width="4" height="46" rx="2" fill="#ffffff" opacity="0.45" clip-path="url(#apd-body)"/>
        <!-- 温度计 -->
        <rect x="147.5" y="34" width="5" height="58" rx="2.5" fill="#fee2e2" stroke="#ef4444" stroke-width="1.1"/>
        <circle cx="150" cy="34" r="5" fill="#ef4444"/>
        <text x="166" y="48" font-size="10" fill="#334155">温度计</text>
        <text x="166" y="60" font-size="8.5" fill="#b45309">水银球在支管口</text>
        <!-- 支管 -->
        <path d="M 161 92 L 234 92" stroke="#60a5fa" stroke-width="6" stroke-linecap="round"/>
        <path d="M 161 92 L 234 92" stroke="#f0f9ff" stroke-width="2" stroke-linecap="round"/>
        <!-- 冷凝管 -->
        <g filter="url(#apd-shadow)">
          <rect x="228" y="72" width="106" height="42" rx="12" fill="url(#apd-glass)" stroke="#5eead4" stroke-width="2.2"/>
        </g>
        <path d="M 236 93 L 326 93" stroke="#99f6e4" stroke-width="7" stroke-linecap="round"/>
        <path d="M 236 93 L 326 93" stroke="#f0fdfa" stroke-width="2" stroke-linecap="round"/>
        <rect x="234" y="77" width="94" height="5" rx="2.5" fill="#ffffff" opacity="0.75"/>
        <text x="246" y="66" font-size="9" fill="#0f766e">冷凝管</text>
        <text x="336" y="86" font-size="8" fill="#2563eb">↓冷水</text>
        <text x="336" y="122" font-size="8" fill="#dc2626">↑热水</text>
        <!-- 接收瓶 -->
        <g filter="url(#apd-shadow)">
          <path d="M 324 138 L 314 176 L 354 176 L 344 138 Z" fill="url(#apd-glass)" stroke="#60a5fa" stroke-width="2"/>
        </g>
        <path d="M 317 166 L 351 166 L 353 175 L 315 175 Z" fill="url(#apd-liquid)" opacity="0.75"/>
        <text x="304" y="194" font-size="9" fill="#334155">接收瓶</text>
        <!-- 酒精灯 -->
        <circle cx="150" cy="158" r="34" fill="url(#apd-flameGlow)"/>
        <path d="M 130 200 L 170 200 L 161 178 L 139 178 Z" fill="url(#apd-stand)" opacity="0.35"/>
        <path d="M 130 200 L 170 200 L 161 178 L 139 178 Z" fill="#fde68a" stroke="#d97706" stroke-width="1.4"/>
        <path d="M 132 198 L 150 198 L 150 180 L 140 180 Z" fill="#ffffff" opacity="0.4"/>
        <path d="M 150 176 q -11 -15 0 -28 q 11 13 0 28 Z" fill="url(#apd-flame)" filter="url(#apd-glow)"/>
        <path d="M 150 172 q -5 -8 0 -15 q 5 7 0 15 Z" fill="#fefce8" opacity="0.9"/>
        <ellipse cx="150" cy="174" rx="7" ry="3" fill="#fbbf24"/>
        <rect x="0" y="0" width="360" height="220" filter="url(#apd-grain)" opacity="0.4"/>
      </svg>
      <p class="apparatus-label">蒸馏装置</p>
    </div>`,

  extraction: `
    <div class="apparatus-item">
      <svg viewBox="0 0 280 240" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="萃取分液装置示意图">
        ${svgCommonDefs('ape')}
        <defs><clipPath id="ape-cone"><path d="M 108 96 L 172 96 L 146 150 Q 140 158 134 150 Z"/></clipPath></defs>
        <!-- 铁架台 -->
        <rect x="36" y="16" width="5" height="206" rx="2.5" fill="url(#ape-stand)"/>
        <rect x="10" y="220" width="60" height="7" rx="3.5" fill="url(#ape-stand)"/>
        <rect x="39" y="76" width="150" height="5" rx="2.5" fill="url(#ape-stand)"/>
        <!-- 分液漏斗 -->
        <g filter="url(#ape-shadow)">
          <path d="M 106 94 L 174 94 L 146 150 Q 140 158 134 150 Z" fill="url(#ape-glass)" stroke="#60a5fa" stroke-width="2.2" stroke-linejoin="round"/>
          <rect x="134" y="44" width="12" height="52" fill="url(#ape-glass)" stroke="#60a5fa" stroke-width="2.2"/>
        </g>
        <rect x="108" y="96" width="64" height="34" fill="#fde68a" opacity="0.9" clip-path="url(#ape-cone)"/>
        <rect x="108" y="130" width="64" height="30" fill="#bae6fd" opacity="0.85" clip-path="url(#ape-cone)"/>
        <rect x="108" y="96" width="64" height="64" fill="url(#ape-liquidHi)" clip-path="url(#ape-cone)" opacity="0.55"/>
        <rect x="112" y="48" width="4" height="44" rx="2" fill="url(#ape-sheen)" opacity="0.9"/>
        <rect x="117" y="102" width="5" height="44" rx="2.5" fill="#ffffff" opacity="0.5" clip-path="url(#ape-cone)"/>
        <line x1="112" y1="130" x2="168" y2="130" stroke="#d97706" stroke-width="1.2" stroke-dasharray="3 2"/>
        <text x="180" y="116" font-size="9" fill="#92400e">有机相（上层）</text>
        <text x="180" y="148" font-size="9" fill="#0369a1">水相（下层）</text>
        <text x="78" y="48" font-size="9" fill="#334155">分液漏斗</text>
        <!-- 活塞 -->
        <rect x="124" y="168" width="32" height="8" rx="3" fill="#64748b"/>
        <circle cx="140" cy="172" r="7" fill="#94a3b8" stroke="#64748b"/>
        <!-- 漏斗颈 -->
        <rect x="137" y="156" width="6" height="34" fill="url(#ape-glass)" stroke="#60a5fa" stroke-width="1.6"/>
        <!-- 液滴 -->
        <ellipse cx="140" cy="196" rx="2.6" ry="4.5" fill="#7dd3fc"/>
        <!-- 烧杯 -->
        <g filter="url(#ape-shadow)">
          <path d="M 104 196 L 104 226 Q 104 230 108 230 L 172 230 Q 176 230 176 226 L 176 196" fill="url(#ape-glass)" stroke="#94a3b8" stroke-width="2"/>
        </g>
        <path d="M 106 214 L 174 214 L 174 226 Q 174 229 171 229 L 109 229 Q 106 229 106 226 Z" fill="url(#ape-liquid)" opacity="0.8"/>
        <rect x="108" y="204" width="64" height="26" fill="url(#ape-liquidHi)" opacity="0.5"/>
        <rect x="107" y="200" width="4" height="26" rx="2" fill="#ffffff" opacity="0.6"/>
        <text x="112" y="210" font-size="9" fill="#334155">烧杯</text>
        <rect x="0" y="0" width="280" height="240" filter="url(#ape-grain)" opacity="0.4"/>
      </svg>
      <p class="apparatus-label">萃取·分液装置</p>
    </div>`,

  filtration: `
    <div class="apparatus-item">
      <svg viewBox="0 0 280 220" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="40" y1="20" x2="40" y2="200" stroke="#475569" stroke-width="3"/>
        <line x1="15" y1="200" x2="65" y2="200" stroke="#475569" stroke-width="4"/>
        <line x1="40" y1="80" x2="180" y2="80" stroke="#475569" stroke-width="2"/>
        <!-- 漏斗 -->
        <path d="M 120 60 L 160 60 L 145 110 L 145 140 L 135 140 L 135 110 Z" fill="#f0fdfa" stroke="#0d9488" stroke-width="2"/>
        <!-- 滤纸 -->
        <path d="M 122 65 L 158 65 L 146 108 L 134 108 Z" fill="none" stroke="#94a3b8" stroke-width="1" stroke-dasharray="3 2"/>
        <!-- 漏斗颈 -->
        <line x1="140" y1="140" x2="140" y2="165" stroke="#0d9488" stroke-width="2"/>
        <!-- 烧杯（接收） -->
        <path d="M 100 165 L 100 200 L 180 200 L 180 165" fill="none" stroke="#94a3b8" stroke-width="2"/>
        <line x1="140" y1="165" x2="140" y2="175" stroke="#0d9488" stroke-width="2"/>
        <text x="110" y="190" font-size="9" fill="#475569">滤液</text>
        <!-- 玻璃棒 -->
        <line x1="120" y1="30" x2="150" y2="65" stroke="#a16207" stroke-width="3"/>
        <text x="100" y="40" font-size="9" fill="#475569">玻璃棒引流</text>
        <!-- 待过滤液 -->
        <ellipse cx="135" cy="55" rx="10" ry="3" fill="#fde68a"/>
      </svg>
      <p class="apparatus-label">过滤装置</p>
    </div>`,

  evaporation: `
    <div class="apparatus-item">
      <svg viewBox="0 0 240 220" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="30" y1="20" x2="30" y2="200" stroke="#475569" stroke-width="3"/>
        <line x1="10" y1="200" x2="50" y2="200" stroke="#475569" stroke-width="4"/>
        <!-- 铁圈 -->
        <line x1="30" y1="130" x2="160" y2="130" stroke="#475569" stroke-width="2"/>
        <!-- 蒸发皿 -->
        <path d="M 80 120 Q 120 145 160 120 L 155 110 L 85 110 Z" fill="#fef3c7" stroke="#d97706" stroke-width="2"/>
        <!-- 溶液 -->
        <path d="M 88 118 Q 120 135 152 118 Z" fill="#bae6fd"/>
        <!-- 玻璃棒 -->
        <line x1="130" y1="80" x2="110" y2="120" stroke="#a16207" stroke-width="3"/>
        <text x="135" y="85" font-size="9" fill="#475569">玻璃棒搅拌</text>
        <!-- 酒精灯 -->
        <path d="M 95 200 L 145 200 L 135 170 L 105 170 Z" fill="#f59e0b" stroke="#d97706" stroke-width="1.5"/>
        <ellipse cx="120" cy="162" rx="14" ry="7" fill="#ef4444"/>
        <!-- 蒸汽 -->
        <path d="M 110 100 Q 100 85 110 70" fill="none" stroke="#94a3b8" stroke-width="1.5" stroke-dasharray="3 3"/>
        <path d="M 125 95 Q 135 80 125 65" fill="none" stroke="#94a3b8" stroke-width="1.5" stroke-dasharray="3 3"/>
        <text x="140" y="75" font-size="9" fill="#94a3b8">蒸汽</text>
      </svg>
      <p class="apparatus-label">蒸发结晶装置</p>
    </div>`,

  titration: `
    <div class="apparatus-item">
      <svg viewBox="0 0 260 240" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="30" y1="20" x2="30" y2="220" stroke="#475569" stroke-width="3"/>
        <line x1="10" y1="220" x2="50" y2="220" stroke="#475569" stroke-width="4"/>
        <!-- 滴定管夹 -->
        <line x1="30" y1="50" x2="120" y2="50" stroke="#475569" stroke-width="2"/>
        <!-- 滴定管 -->
        <rect x="100" y="40" width="18" height="120" fill="#f0fdfa" stroke="#0d9488" stroke-width="1.5"/>
        <line x1="100" y1="60" x2="118" y2="60" stroke="#94a3b8" stroke-width="0.5"/>
        <line x1="100" y1="80" x2="118" y2="80" stroke="#94a3b8" stroke-width="0.5"/>
        <line x1="100" y1="100" x2="118" y2="100" stroke="#94a3b8" stroke-width="0.5"/>
        <line x1="100" y1="120" x2="118" y2="120" stroke="#94a3b8" stroke-width="0.5"/>
        <!-- 活塞 -->
        <rect x="95" y="158" width="28" height="8" fill="#475569"/>
        <!-- 尖嘴 -->
        <path d="M 106 166 L 112 166 L 109 178 Z" fill="#f0fdfa" stroke="#0d9488" stroke-width="1"/>
        <!-- 标准液 -->
        <rect x="101" y="60" width="16" height="96" fill="#bae6fd"/>
        <text x="125" y="80" font-size="9" fill="#0369a1">标准液</text>
        <!-- 液滴 -->
        <ellipse cx="109" cy="183" rx="3" ry="5" fill="#bae6fd"/>
        <!-- 锥形瓶 -->
        <path d="M 80 190 L 90 215 L 130 215 L 140 190 Z" fill="#fef3c7" stroke="#d97706" stroke-width="2"/>
        <ellipse cx="110" cy="190" rx="30" ry="6" fill="#fef3c7" stroke="#d97706" stroke-width="1.5"/>
        <text x="70" y="210" font-size="9" fill="#475569">锥形瓶</text>
        <text x="70" y="222" font-size="8" fill="#92400e">(待测液)</text>
      </svg>
      <p class="apparatus-label">滴定装置</p>
    </div>`,

  airtightness: `
    <div class="apparatus-item">
      <svg viewBox="0 0 300 200" xmlns="http://www.w3.org/2000/svg">
        <!-- 试管 -->
        <rect x="80" y="40" width="50" height="100" rx="6" fill="#f0fdfa" stroke="#0d9488" stroke-width="2"/>
        <rect x="75" y="35" width="60" height="12" rx="3" fill="#94a3b8" stroke="#475569" stroke-width="1.5"/>
        <!-- 导管 -->
        <path d="M 105 35 L 105 15 L 190 15 L 190 90" fill="none" stroke="#475569" stroke-width="2.5"/>
        <!-- 手（微热） -->
        <ellipse cx="105" cy="120" rx="35" ry="25" fill="none" stroke="#fbbf24" stroke-width="2" stroke-dasharray="4 3"/>
        <text x="55" y="125" font-size="10" fill="#d97706">手握微热</text>
        <!-- 烧杯 -->
        <path d="M 155 90 L 155 160 L 225 160 L 225 90" fill="none" stroke="#94a3b8" stroke-width="2"/>
        <!-- 水 -->
        <rect x="156" y="120" width="68" height="39" fill="#bae6fd" opacity="0.6"/>
        <text x="165" y="150" font-size="9" fill="#0369a1">水</text>
        <!-- 气泡 -->
        <circle cx="190" cy="105" r="4" fill="none" stroke="#2563eb" stroke-width="1.5"/>
        <circle cx="185" cy="115" r="3" fill="none" stroke="#2563eb" stroke-width="1.5"/>
        <text x="165" y="180" font-size="10" fill="#15803d">✓ 气泡冒出 = 气密性好</text>
      </svg>
      <p class="apparatus-label">气密性检查（微热法）</p>
    </div>`,

  gas: `
    <div class="apparatus-item">
      <svg viewBox="0 0 300 200" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="30" y1="20" x2="30" y2="180" stroke="#475569" stroke-width="3"/>
        <line x1="10" y1="180" x2="50" y2="180" stroke="#475569" stroke-width="4"/>
        <!-- 试管 -->
        <rect x="70" y="50" width="45" height="90" rx="6" fill="#f0fdfa" stroke="#0d9488" stroke-width="2" transform="rotate(-15 92 95)"/>
        <!-- 导管 -->
        <path d="M 115 45 L 115 20 L 230 20 L 230 80" fill="none" stroke="#475569" stroke-width="2.5"/>
        <!-- 集气瓶 -->
        <rect x="200" y="80" width="60" height="80" rx="4" fill="#f0fdfa" stroke="#0d9488" stroke-width="2"/>
        <rect x="201" y="90" width="58" height="68" fill="#bae6fd" opacity="0.5"/>
        <text x="210" y="165" font-size="9" fill="#0369a1">水</text>
        <text x="215" y="120" font-size="9" fill="#475569">气体</text>
        <!-- 酒精灯 -->
        <path d="M 75 170 L 120 170 L 110 145 L 85 145 Z" fill="#f59e0b" stroke="#d97706" stroke-width="1.5"/>
        <ellipse cx="97" cy="138" rx="14" ry="7" fill="#ef4444"/>
      </svg>
      <p class="apparatus-label">气体制备·收集装置</p>
    </div>`,
};

/* 9. 3D 分子结构查看器（3Dmol.js） */
let _molViewerCounter = 0;

function moleculeViewerHtml(molBlocks, substanceMap) {
  if (!molBlocks || !Object.keys(molBlocks).length) return '';

  const cards = Object.keys(molBlocks).map((label) => {
    const molblock = molBlocks[label];
    const formula = substanceMap?.[label] || '';
    const viewerId = `molviewer-${++_molViewerCounter}`;
    return `
      <div class="mol-card">
        <div class="mol-card-head">
          <span class="mol-label">${esc(label)}</span>
          ${formula ? `<span class="mol-formula">${chem(formula)}</span>` : ''}
        </div>
        <div class="mol-viewer" id="${viewerId}"></div>
        <script type="text/molecule-data" data-viewer="${viewerId}">${molblock}</script>
        <div class="mol-controls">
          <button class="mol-btn" data-action="spin" data-viewer="${viewerId}">🔄 旋转</button>
          <button class="mol-btn" data-action="reset" data-viewer="${viewerId}">🎯 重置</button>
        </div>
      </div>`;
  }).join('');

  return `
  <div class="viz-block">
    <h5>🧬 分子结构（可拖拽旋转）</h5>
    <div class="mol-grid">${cards}</div>
  </div>`;
}

/* 初始化所有分子查看器（在渲染后调用） */
function initMoleculeViewers() {
  if (typeof $3Dmol === 'undefined') return;

  document.querySelectorAll('.mol-viewer').forEach((el) => {
    const id = el.id;
    if (el.dataset.initialized) return;
    const script = el.parentElement.querySelector(`script[data-viewer="${id}"]`);
    if (!script) return;
    const molblock = script.textContent;

    try {
      const viewer = $3Dmol.createViewer(el, { backgroundColor: 'white' });
      viewer.addModel(molblock, 'mol');
      viewer.setStyle({}, { stick: { radius: 0.2 }, sphere: { scale: 0.3 } });
      viewer.zoomTo();
      viewer.render();
      el.dataset.initialized = '1';
      el._viewer = viewer;
    } catch (e) {
      el.innerHTML = '<p class="mol-error">3D 渲染失败</p>';
    }
  });

  // 绑定控制按钮
  document.querySelectorAll('.mol-btn').forEach((btn) => {
    if (btn.dataset.bound) return;
    btn.dataset.bound = '1';
    btn.addEventListener('click', () => {
      const id = btn.dataset.viewer;
      const el = document.getElementById(id);
      const viewer = el?._viewer;
      if (!viewer) return;
      if (btn.dataset.action === 'spin') {
        viewer.spin(!viewer.spin());
      } else if (btn.dataset.action === 'reset') {
        viewer.zoomTo();
        viewer.render();
      }
    });
  });
}

/* 讲：逐步讲解 + 知识点溯源 */
function explainHtml(e) {
  const overview = e.oneLineOverview ? `
    <div class="overview">
      <span class="overview-tag">一句话总览</span>
      <p>${esc(e.oneLineOverview)}</p>
    </div>` : '';

  const pitfalls = (e.pitfalls && e.pitfalls.length) ? `
    <div class="pitfall-block">
      <h4>⚠ 易错点</h4>
      <ul>${e.pitfalls.map((p) => `<li>${chem(p)}</li>`).join('')}</ul>
    </div>` : '';

  const refs = e.references.length ? `
    <h4>📚 知识点卡片（点击展开要点与误区）</h4>
    <div class="refs">
      ${e.references.map((r) => `
        <details class="ref-card">
          <summary><b>${esc(r.name)}</b><span class="mono">${esc(r.id)}</span><span>${esc(r.textbookRef)}</span></summary>
          ${r.summary ? `<div class="ref-summary"><b>要点：</b>${chem(r.summary)}</div>` : ''}
          ${r.pitfall ? `<div class="ref-pitfall"><b>易错：</b>${chem(r.pitfall)}</div>` : ''}
        </details>`).join('')}
    </div>` : '';

  return `
    <p class="panel-note">讲解由引擎依据求解步骤与知识库自动生成，全部结论可溯源到教材知识点。</p>
    ${overview}
    <ol class="steps">
      ${e.steps.map((st) => `
        <li class="${st.isKeyStep ? 'key-step' : ''}">
          <div class="step-body">
            ${st.isKeyStep ? '<span class="key-badge" title="解题突破口">🔑</span>' : ''}
            ${chem(st.text)}
          </div>
          ${st.why ? `<details class="why"><summary>为什么</summary><p>${chem(st.why)}</p></details>` : ''}
          ${st.evidence ? `<div class="chips"><span class="chip mono">${esc(st.evidence)}</span></div>` : ''}
        </li>`).join('')}
    </ol>
    ${pitfalls}
    ${refs}`;
}

/* 引导：分级启发提问（伴思模式，逐级解锁）
   每层标注「认知层级」与「互动模式」，并支持 反问→提示→知识点 的逐级降级展开。 */
function guideHtml(g) {
  const layerMeta = {
    Knowledge:   { label: '知识点', icon: '📖', cls: 'ly-knowledge', desc: '陈述性知识，直接给' },
    Application: { label: '知识运用', icon: '🛠️', cls: 'ly-application', desc: '给方向，自己选怎么用' },
    Ability:     { label: '能力', icon: '🧠', cls: 'ly-ability', desc: '只反问，自己想策略' },
  };
  const interMeta = {
    Probe:  { label: '反问', cls: 'im-probe' },
    Hint:   { label: '提示', cls: 'im-hint' },
    Reveal: { label: '告知', cls: 'im-reveal' },
  };
  return `
    <p class="panel-note guide-note">
      伴思模式：只引导、不给答案——按「审题分类→建立模型→知识检索→规则应用→精确求解→守恒校验」六步框架逐级思考。
      每层标注认知层级（<b>知识点 / 知识运用 / 能力</b>）：知识点直接给，知识运用给方向，能力只反问。卡住时点「💡 给我提示」逐级降级。
    </p>
    <div class="guide-toolbar">
      <button class="guide-expand-btn" onclick="toggleAllGuideLevels(this)">全部展开</button>
      <span class="guide-legend">
        <span class="legend-chip ly-knowledge">📖 知识点</span>
        <span class="legend-chip ly-application">🛠️ 知识运用</span>
        <span class="legend-chip ly-ability">🧠 能力</span>
      </span>
    </div>
    <div class="levels">
      ${g.levels.map((lv, i) => {
        const open = i === 0 ? 'open' : '';
        const lm = layerMeta[lv.cognitiveLayer] || layerMeta.Ability;
        const im = interMeta[lv.interaction] || interMeta.Probe;
        const hintBlock = lv.hint ? `
          <div class="guide-hint">
            <button type="button" class="hint-toggle" onclick="toggleGuideHint(this)">💡 给我提示</button>
            <div class="hint-body" hidden>${esc(lv.hint)}</div>
          </div>` : '';
        const cardBlock = lv.knowledgeCard ? `
          <div class="guide-kcard">
            <div class="kcard-head">
              <span class="kcard-name">${esc(lv.knowledgeCard.name || '')}</span>
              ${lv.knowledgeCard.textbookRef ? `<span class="kcard-ref">${esc(lv.knowledgeCard.textbookRef)}</span>` : ''}
            </div>
            ${lv.knowledgeCard.summary ? `<p class="kcard-summary">${esc(lv.knowledgeCard.summary)}</p>` : ''}
          </div>` : '';
        return `
        <details class="level level-${lm.cls}" ${open}>
          <summary class="level-head">
            <span class="level-no">${lv.level}</span>
            <h5>${esc(lv.title)}</h5>
            <span class="layer-badge ${lm.cls}" title="${esc(lm.desc)}">${lm.icon} ${lm.label}</span>
            <span class="inter-badge ${im.cls}">${im.label}</span>
          </summary>
          <ul>${lv.prompts.map((p) => `<li>${chem(p)}</li>`).join('')}</ul>
          ${cardBlock}
          ${hintBlock}
        </details>`;
      }).join('')}
    </div>`;
}

/* 引导面板：全部展开/收起切换 */
function toggleAllGuideLevels(btn) {
  const panel = btn.closest('.panel') || document;
  const details = panel.querySelectorAll('.level');
  const allOpen = Array.from(details).every((d) => d.open);
  details.forEach((d) => { d.open = !allOpen; });
  btn.textContent = allOpen ? '全部展开' : '全部收起';
}

/* 引导：单步提示的展开/收起 */
function toggleGuideHint(btn) {
  const body = btn.nextElementSibling;
  const show = body.hasAttribute('hidden');
  if (show) {
    body.removeAttribute('hidden');
    btn.textContent = '收起提示';
  } else {
    body.setAttribute('hidden', '');
    btn.textContent = '💡 给我提示';
  }
}

/* ---------- 知识库浏览页 ---------- */

const MODULE_LABEL = {
  Inorganic: '无机化学',
  Organic: '有机化学',
  ReactionPrinciples: '反应原理',
  Experiment: '化学实验',
  Structure: '物质结构',
  Technology: '化学与技术',
};

function renderKnowledgePage() {
  const view = document.getElementById('knowledgeView');
  if (!KB || KB.items.length === 0) {
    view.innerHTML = '<p class="empty">知识库数据未加载。</p>';
    return;
  }
  const modules = [...new Set(KB.items.map((i) => i.module))];
  view.innerHTML = `
    <div class="kb-header">
      <h2>📚 知识库（${KB.items.length} 条知识点 · ${KB.rules.length} 条规则）</h2>
      <p class="kb-hint">支持关键词搜索（名称/要点/易错点），点击卡片查看前置依赖与关联规则。</p>
    </div>
    <div class="kb-filters" id="kbFilters">
      <button class="kb-filter active" data-mod="all">全部</button>
      ${modules.map((m) => `<button class="kb-filter" data-mod="${esc(m)}">${MODULE_LABEL[m] || m}</button>`).join('')}
    </div>
    <div class="kb-grid" id="kbGrid"></div>
  `;
  view.querySelectorAll('.kb-filter').forEach((btn) => {
    btn.addEventListener('click', () => {
      view.querySelectorAll('.kb-filter').forEach((b) => b.classList.toggle('active', b === btn));
      renderKnowledgeList();
    });
  });
  renderKnowledgeList();
}

function renderKnowledgeList() {
  const grid = document.getElementById('kbGrid');
  if (!grid) return;
  const activeMod = document.querySelector('.kb-filter.active')?.dataset.mod || 'all';
  let items = KB.items;
  if (activeMod !== 'all') items = items.filter((i) => i.module === activeMod);
  if (keyword) {
    items = items.filter((i) =>
      (i.name + i.summary + i.pitfall + i.textbookRef + i.id).toLowerCase().includes(keyword));
  }
  if (items.length === 0) {
    grid.innerHTML = '<p class="empty">没有匹配的知识点。</p>';
    return;
  }
  grid.innerHTML = items.map((item) => knowledgeCardHtml(item)).join('');
  grid.querySelectorAll('.kb-card').forEach((card) => {
    card.addEventListener('click', (e) => {
      if (e.target.closest('.kb-prereq') || e.target.closest('.kb-rule')) return;
      card.classList.toggle('expanded');
    });
  });
}

function knowledgeCardHtml(item) {
  const prereqs = (item.prerequisites || []).map((id) => {
    const ref = KB.items.find((i) => i.id === id);
    return ref
      ? `<span class="kb-prereq" onclick="jumpToKnowledge('${esc(id)}')">${esc(ref.name)}</span>`
      : `<span class="kb-prereq">${esc(id)}</span>`;
  }).join('');
  const rules = (item.rules || []).map((r) =>
    `<span class="kb-rule" title="${esc(r.display)}"><b>${esc(r.id)}</b> ${esc(r.name)}</span>`
  ).join('');
  return `
    <div class="kb-card" data-id="${esc(item.id)}">
      <div class="kb-card-head">
        <span class="kb-id">${esc(item.id)}</span>
        <span class="kb-tier">${item.tier === 1 ? '主干' : item.tier === 2 ? '拓展' : '选学'}</span>
      </div>
      <h4 class="kb-name">${esc(item.name)}</h4>
      <p class="kb-module">${MODULE_LABEL[item.module] || item.module} · ${esc(item.textbookRef)}</p>
      <p class="kb-summary">${esc(item.summary || '')}</p>
      ${item.pitfall ? `<p class="kb-pitfall">⚠ ${esc(item.pitfall)}</p>` : ''}
      <div class="kb-expand">
        ${prereqs ? `<div class="kb-section"><b>前置知识：</b>${prereqs}</div>` : ''}
        ${rules ? `<div class="kb-section"><b>关联规则：</b>${rules}</div>` : ''}
      </div>
    </div>`;
}

function jumpToKnowledge(id) {
  keyword = '';
  document.getElementById('searchBox').value = '';
  switchView('knowledge');
  setTimeout(() => {
    const card = document.querySelector(`.kb-card[data-id="${id}"]`);
    if (card) {
      card.classList.add('expanded');
      card.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, 100);
}

/* ---------- 知识问答面板 ---------- */

function renderQAPanel() {
  const view = document.getElementById('qaView');
  view.innerHTML = `
    <div class="qa-header">
      <h2>❓ 化学知识问答</h2>
      <p class="kb-hint">输入化学问题，系统从知识库（${KB.items.length} 条）与规则库（${KB.rules.length} 条）中检索匹配内容。基于关键词匹配，非 AI 对话。</p>
    </div>
    <div class="qa-input-row">
      <input id="qaInput" class="search qa-input" type="search" placeholder="例如：什么是勒夏特列原理？苯能使溴水褪色吗？电子守恒怎么用？" />
      <button id="qaBtn" class="guide-expand-btn">搜索</button>
    </div>
    <div class="qa-quick">
      <span>试试：</span>
      ${['勒夏特列', '苯 溴水', '电子守恒', '化学平衡', '电化学', '滴定误差'].map((q) =>
        `<button class="qa-quick-btn" data-q="${esc(q)}">${esc(q)}</button>`).join('')}
    </div>
    <div id="qaResults" class="qa-results"></div>
  `;
  const input = document.getElementById('qaInput');
  const btn = document.getElementById('qaBtn');
  const doSearch = () => { document.getElementById('qaResults').innerHTML = qaSearchHtml(input.value.trim()); };
  btn.addEventListener('click', doSearch);
  input.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });
  view.querySelectorAll('.qa-quick-btn').forEach((b) => {
    b.addEventListener('click', () => { input.value = b.dataset.q; doSearch(); });
  });
}

function qaSearchHtml(query) {
  if (!query) return '<p class="empty">请输入问题关键词。</p>';
  const tokens = qaTokenize(query);
  const kHits = [];
  const rHits = [];
  for (const item of KB.items) {
    const s = scoreKnowledge(item, tokens);
    if (s > 0) kHits.push({ item, score: s });
  }
  for (const rule of KB.rules) {
    const s = scoreRule(rule, tokens);
    if (s > 0) rHits.push({ rule, score: s });
  }
  kHits.sort((a, b) => b.score - a.score);
  rHits.sort((a, b) => b.score - a.score);

  if (kHits.length === 0 && rHits.length === 0) {
    return `<p class="empty">未找到与「${esc(query)}」相关的知识点，请尝试其他关键词。</p>`;
  }

  let html = '';
  if (kHits.length > 0) {
    html += `<h3 class="qa-section-title">📖 相关知识点（${kHits.length}）</h3>`;
    html += kHits.slice(0, 10).map(({ item }) => knowledgeCardHtml(item)).join('');
  }
  if (rHits.length > 0) {
    html += `<h3 class="qa-section-title">📐 相关规则（${rHits.length}）</h3>`;
    html += '<div class="qa-rules">' + rHits.slice(0, 15).map(({ rule }) => `
      <div class="qa-rule-card">
        <div class="qa-rule-head"><span class="kb-id">${esc(rule.id)}</span> ${esc(rule.name)}</div>
        <p class="qa-rule-display">${chem(rule.display || '')}</p>
        ${rule.knowledgeId ? `<p class="qa-rule-ref">所属知识点：<span class="kb-prereq" onclick="jumpToKnowledge('${esc(rule.knowledgeId)}')">${esc(KB.items.find(i=>i.id===rule.knowledgeId)?.name || rule.knowledgeId)}</span></p>` : ''}
      </div>
    `).join('') + '</div>';
  }
  return html;
}

function qaTokenize(query) {
  const tokens = [query];
  if (query.length > 4) {
    for (let i = 0; i <= query.length - 2; i++) tokens.push(query.slice(i, i + 2));
  }
  const parts = query.split(/\s+/).filter(Boolean);
  if (parts.length > 1) tokens.push(...parts);
  return [...new Set(tokens.map((t) => t.toLowerCase()))];
}

function scoreKnowledge(item, tokens) {
  const fields = [
    { text: item.name, weight: 10 },
    { text: item.id, weight: 5 },
    { text: item.pitfall, weight: 4 },
    { text: item.summary, weight: 3 },
    { text: item.textbookRef, weight: 2 },
  ];
  for (const f of fields) {
    const score = tokens.reduce((s, t) => s + ((f.text || '').toLowerCase().includes(t) ? f.weight : 0), 0);
    if (score > 0) return score;
  }
  return 0;
}

function scoreRule(rule, tokens) {
  const nameScore = tokens.reduce((s, t) => s + ((rule.name || '').toLowerCase().includes(t) ? 10 : 0), 0);
  if (nameScore > 0) return nameScore;
  const idScore = tokens.reduce((s, t) => s + ((rule.id || '').toLowerCase().includes(t) ? 5 : 0), 0);
  if (idScore > 0) return idScore;
  return tokens.reduce((s, t) => s + ((rule.display || '').toLowerCase().includes(t) ? 3 : 0), 0);
}

/* ---------- 错误提示 ---------- */

function showError(msg) {
  document.getElementById('loadingTip').hidden = true;
  const el = document.getElementById('errorTip');
  el.hidden = false;
  el.textContent = msg;
}

/* ---------- 实时求解（调用 /api/gateway/solve，独立视图，不影响离线题库） ---------- */

const LIVE_STATE_BADGE = {
  Confirmed: { text: '已确认', cls: 'ok' },
  CandidateSet: { text: '候选结论', cls: 'warn' },
  Insufficient: { text: '条件不足', cls: 'err' },
};

function defaultLiveApiBase() {
  try { return localStorage.getItem('liveApiBase') || 'http://localhost:5080'; }
  catch (e) { return 'http://localhost:5080'; }
}

function renderLivePanel() {
  simStopEmbedded();
  const host = document.getElementById('liveView');
  host.innerHTML = `
    <div class="live-solve">
      <div class="panel">
        <h2 style="margin-top:0">🧪 实时求解</h2>
        <p class="panel-note">输入化学题，调用 <code>/api/gateway/solve</code> 实时返回引擎结论与结构化辅助数据（数值 / 粒子排序 / 守恒式 / pH / 反应轨迹）。结论字段按学生面规则裁剪，<b>不参与判分</b>。</p>
        <p class="panel-note">
          <label>API 地址：
            <input id="liveApiBase" class="live-input" value="${esc(defaultLiveApiBase())}" spellcheck="false" />
          </label>
        </p>
        <textarea id="liveInput" class="live-textarea" placeholder="在此粘贴题目文本，例如：&#10;500 mL NaOH 溶液中含有 0.5 mol NaOH，该溶液的物质的量浓度是多少？"></textarea>
        <div class="live-actions">
          <button id="liveSolveBtn" class="hero-cta" type="button">求解</button>
          <button id="liveClearBtn" class="hero-cta hero-cta-ghost" type="button">清空</button>
          <span id="liveStatus" class="panel-note"></span>
        </div>
      </div>
      <div id="liveResult"></div>
    </div>`;
  document.getElementById('liveSolveBtn').addEventListener('click', doLiveSolve);
  document.getElementById('liveClearBtn').addEventListener('click', () => {
    document.getElementById('liveInput').value = '';
    document.getElementById('liveResult').innerHTML = '';
    document.getElementById('liveStatus').textContent = '';
  });
  const baseInput = document.getElementById('liveApiBase');
  baseInput.addEventListener('change', () => {
    try { localStorage.setItem('liveApiBase', baseInput.value.trim()); } catch (e) {}
  });
  document.getElementById('liveInput').addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') doLiveSolve();
  });
}

async function doLiveSolve() {
  const text = document.getElementById('liveInput').value.trim();
  const statusEl = document.getElementById('liveStatus');
  const resultEl = document.getElementById('liveResult');
  if (!text) { statusEl.textContent = '请输入题目文本。'; return; }
  const base = document.getElementById('liveApiBase').value.trim().replace(/\/+$/, '');
  statusEl.textContent = '正在调用引擎…';
  resultEl.innerHTML = '';
  try {
    const res = await fetch(base + '/api/gateway/solve', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text }),
    });
    if (!res.ok) {
      statusEl.textContent = '请求失败：HTTP ' + res.status;
      return;
    }
    const view = await res.json();
    statusEl.textContent = '完成（结论：' + (view.conclusion_state || '?') + '）';
    resultEl.innerHTML = renderLiveResult(view);
    initLiveSimPlayer(resultEl, view);
  } catch (err) {
    statusEl.textContent = '调用失败：' + err.message +
      '（请确认后端服务已启动且 API 地址正确；浏览器需允许跨域）';
  }
}

function renderLiveResult(view) {
  const st = LIVE_STATE_BADGE[view.conclusion_state] || { text: view.conclusion_state || '未知', cls: 'warn' };
  const notConfirmed = view.conclusion_state && view.conclusion_state !== 'Confirmed';
  const s = view.structured;
  const hasStructured = !!(s && (s.numeric || s.aqueous || s.equilibrium || s.reaction_trace));

  let html = `<div class="panel"><div class="badges">
    <span class="badge st-badge st-${st.cls}">结论：${esc(st.text)}</span>
    <span class="badge cat-badge">任务：${esc(view.task_type || '-')}</span>
  </div>`;
  if (view.final_result) html += `<h4>最终结果</h4><div class="eq-block"><ul><li>${chem(view.final_result)}</li></ul></div>`;
  if (view.constraint_note) html += `<p class="panel-note" style="color:#b45309">⚠ ${esc(view.constraint_note)}</p>`;
  if (view.candidates && view.candidates.length) {
    html += `<h4>候选结论</h4><ul>${view.candidates.map((c) => `<li>${esc(c)}</li>`).join('')}</ul>`;
  }
  if (view.derivation_steps && view.derivation_steps.length) {
    html += `<h4>推导步骤</h4><ol class="steps">${view.derivation_steps.map((x) => `<li><div class="step-body">${esc(x)}</div></li>`).join('')}</ol>`;
  }
  if (view.reasoning_nodes && view.reasoning_nodes.length) html += liveNodesHtml('推理节点', view.reasoning_nodes);
  if (view.explanation_nodes && view.explanation_nodes.length) html += liveNodesHtml('讲解节点', view.explanation_nodes);
  if (view.reply) html += `<h4>回复</h4><div class="step-body">${esc(view.reply)}</div>`;
  html += `</div>`;

  if (hasStructured) {
    html += `<div class="panel"><h4>🧮 结构化辅助数据</h4>`;
    if (notConfirmed) {
      html += `<p class="panel-note" style="color:#b45309">以下为引擎对题给数据的<b>辅助计算结果</b>，不作最终判定结论，仅供参考。</p>`;
    }
    html += structuredHtml(s);
    if (s.warnings && s.warnings.length) {
      html += `<ul class="panel-note">${s.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>`;
    }
    html += `</div>`;
  }
  if (s && s.reaction_trace && s.reaction_trace.events && s.reaction_trace.events.length) {
    html += simPlayerMarkup(s.reaction_trace);
  }
  return html;
}

function liveNodesHtml(title, nodes) {
  const items = nodes.map((n) => {
    const type = n.node_type || n.type || 'step';
    return `<li><div class="step-body"><span class="chip chip-type chip-type-${esc(type)}">${esc(type)}</span> ${esc(n.content || '')}</div></li>`;
  }).join('');
  return `<h4>${esc(title)}</h4><ol class="steps">${items}</ol>`;
}

function structuredHtml(s) {
  let h = '';
  if (s.numeric) {
    const n = s.numeric;
    h += `<div class="eq-block"><h5>定量计算</h5><ul>
      <li><span class="mono">${esc(n.formula || '')}</span> → <b>${n.value ?? '-'}</b> ${esc(n.unit || '')}${n.species ? '（' + chem(n.species) + '）' : ''}</li>
    </ul></div>`;
  }
  if (s.aqueous) {
    const a = s.aqueous;
    let inner = '';
    if (a.ph != null) inner += `<li>pH ≈ <b>${a.ph}</b></li>`;
    if (a.acidity_verdict) inner += `<li>酸碱性：${esc(a.acidity_verdict)}</li>`;
    if (a.ordering && a.ordering.length) inner += `<li>浓度排序：<span class="mono">${a.ordering.map(chem).join(' > ')}</span></li>`;
    if (a.conservation_equations && a.conservation_equations.length) {
      inner += `<li>守恒式：<ul>${a.conservation_equations.map((e) => `<li class="mono">${chem(e)}</li>`).join('')}</ul></li>`;
    }
    if (a.ion_concentrations && Object.keys(a.ion_concentrations).length) {
      const ions = Object.entries(a.ion_concentrations)
        .map(([k, v]) => `<li class="mono">${chem(k)} = ${v}</li>`).join('');
      inner += `<li>离子浓度：<ul>${ions}</ul></li>`;
    }
    h += `<div class="eq-block"><h5>水相离子平衡</h5><ul>${inner}</ul></div>`;
  }
  if (s.equilibrium) {
    const e = s.equilibrium;
    let inner = '';
    if (e.ksp_residual_concentration != null) {
      inner += `<li>Ksp 残留离子浓度：<span class="mono">${e.ksp_residual_concentration}</span>（${esc(e.ksp_residual_ion || '')}）</li>`;
    }
    if (e.conversion_constant_k) inner += `<li>沉淀转化常数 K：<span class="mono">${esc(e.conversion_constant_k)}</span></li>`;
    if (e.conversion_proceeds != null) inner += `<li>沉淀转化是否自发：${e.conversion_proceeds ? '是' : '否'}</li>`;
    if (inner) h += `<div class="eq-block"><h5>沉淀溶解平衡</h5><ul>${inner}</ul></div>`;
  }
  if (s.reaction_trace) {
    const t = s.reaction_trace;
    let events = '';
    if (t.events && t.events.length) {
      events = t.events.map((ev) => `<li><div class="step-body"><span class="chip chip-type chip-type-reaction">反应</span> ${chem(ev.equation || '')}<span class="panel-note">（阶段 ${ev.stage_index}，进度 ${ev.extent}）</span></div></li>`).join('');
    }
    let finalSp = '';
    if (t.final_species && t.final_species.length) {
      finalSp = t.final_species.map((sp) => `<li class="mono">${chem(sp.substance || '')}${sp.moles != null ? '：' + sp.moles + ' mol' : ''}</li>`).join('');
    }
    h += `<div class="eq-block"><h5>反应轨迹 <span class="panel-note">（收敛：${t.converged ? '是' : '否'}）</span></h5><ul>${events}${finalSp ? '<li>最终物种：<ul>' + finalSp + '</ul></li>' : ''}</ul></div>`;
  }
  return h;
}

/* ===================== 反应模拟器（前端控件） =====================
 * 特有图标：SVG 烧瓶 + 上升气泡 + 轨道环，集中由 simFlaskIcon 生成（禁止散改字符）。
 * 运行/停止：.sim-icon.running 触发 CSS 关键帧；状态集中存于 LIVE_SIM（对象，避免闭包旧值），
 * 图标动画与轨迹逐步展开严格同步。
 */

// 模拟器特有图标：running=true 时气泡上升、轨道环旋转、液体脉动
function simFlaskIcon(running, size) {
  const s = size || 36;
  return `<span class="sim-icon${running ? ' running' : ''}" title="反应模拟器">` +
    `<svg viewBox="0 0 64 64" width="${s}" height="${s}" aria-hidden="true" focusable="false">` +
      `<ellipse class="sim-ring" cx="32" cy="30" rx="27" ry="8.5" />` +
      `<path class="sim-flask-neck" d="M27 7 H37 V10 H27 Z" />` +
      `<path class="sim-flask-body" d="M28.5 10 V21 L13 51.5 Q12.5 56 16.5 56 H47.5 Q51.5 56 51 51.5 L35.5 21 V10 Z" />` +
      `<path class="sim-liquid" d="M16.5 44 V51.5 Q16 55 19.5 55 H44.5 Q48 55 47.5 51.5 V44 Q40 47 32 44 Q24 41 16.5 44 Z" />` +
      `<circle class="sim-bubble sim-b1" cx="27" cy="50" r="1.8" />` +
      `<circle class="sim-bubble sim-b2" cx="33" cy="52" r="1.3" />` +
      `<circle class="sim-bubble sim-b3" cx="38" cy="49" r="2.1" />` +
    `</svg></span>`;
}

// 颜色名 -> 色值（用于物种色点）
const SIM_COLORS = {
  '蓝色': '#3b82f6', '浅蓝色': '#60a5fa', '蓝绿色': '#2dd4bf',
  '浅绿色': '#86efac', '绿色': '#22c55e', '黄绿色': '#a3e635',
  '黄色': '#eab308', '浅黄色': '#fde047', '橙黄色': '#fbbf24',
  '红色': '#ef4444', '橙红色': '#f97316', '橙色': '#f97316',
  '紫红色': '#be123c', '红褐色': '#b45309', '棕色': '#92400e',
  '黑色': '#1f2937', '白色': '#ffffff', '无色': '#ffffff',
  '银白色': '#cbd5e1', '灰色': '#94a3b8', '紫色': '#a855f7'
};
function simDot(name) {
  const c = (name && SIM_COLORS[name]) || '#94a3b8';
  return `<span class="sim-dot" style="background:${c}" title="${esc(name || '')}"></span>`;
}
function simSetIconRunning(wrap, running) {
  if (!wrap) return;
  // 外层 .running 触发 .sim-icon-wrap 的外发光关键帧；内层 .sim-icon.running 触发烧瓶转动/气泡/液体
  wrap.classList.toggle('running', running);
  const icon = wrap.querySelector('.sim-icon');
  if (icon) icon.classList.toggle('running', running);
}

// 状态集中管理（避免定时器闭包读到旧值）
const LIVE_SIM = { embedded: null, standalone: null };

/* ---------- 1. 嵌入式播放面板（/solve 返回 reaction_trace 时） ---------- */
function simPlayerMarkup(trace) {
  const events = trace.events || [];
  const evHtml = events.map((ev, i) => {
    const basis = (ev.basis_knowledge_ids && ev.basis_knowledge_ids.length)
      ? `<span class="panel-note"> · 依据：${esc(ev.basis_knowledge_ids.join(', '))}</span>` : '';
    return `<li class="sim-event" data-i="${i}">` +
      `<div class="sim-event-eq"><span class="sim-step-no">${i + 1}</span>${chem(ev.equation || '')}</div>` +
      `<div class="sim-event-meta">阶段 ${ev.stage_index ?? '-'} · 反应进度 ${ev.extent ?? '-'}${basis}</div>` +
      `</li>`;
  }).join('');
  const fin = (trace.final_species || []).map((sp) =>
    `<span class="sim-chip">${chem(sp.substance || '')}${sp.moles != null ? '：' + sp.moles + ' mol' : ''}</span>`
  ).join('');
  return `<div class="panel sim-panel">` +
    `<div class="sim-head">` +
      `<div class="sim-icon-wrap" id="liveSimIcon">${simFlaskIcon(false, 40)}</div>` +
      `<div class="sim-title"><h4 style="margin:0">⚗️ 反应模拟器 <span class="panel-note">（轨迹播放）</span></h4>` +
      `<p class="panel-note" style="margin:2px 0 0">点击播放，烧瓶图标转动与反应步骤逐步展开同步；停止即复位。</p></div>` +
      `<div class="sim-controls"><button class="hero-cta" id="simPlayBtn" type="button">▶ 播放</button>` +
      `<button class="hero-cta hero-cta-ghost" id="simStopBtn" type="button">■ 停止</button></div>` +
    `</div>` +
    `<div class="sim-progress"><div class="sim-progress-bar" id="simProgressBar"></div></div>` +
    `<ol class="sim-events" id="simEvents">${evHtml}</ol>` +
    `<div class="sim-final" id="simFinal" hidden><div class="sim-final-label">最终物种</div><div class="sim-chips">${fin || '<span class="panel-note">无</span>'}</div></div>` +
  `</div>`;
}

function simStopEmbeddedVisual(state) {
  if (!state) return;
  if (state.timer) { clearInterval(state.timer); state.timer = null; }
  simSetIconRunning(state.iconWrap, false);
  if (state.bar) state.bar.style.width = '0%';
  if (state.eventEls) state.eventEls.forEach((el) => el.classList.remove('on'));
  if (state.finalEl) { state.finalEl.hidden = true; state.finalEl.classList.remove('show'); }
}
function simStopEmbedded() {
  simStopEmbeddedVisual(LIVE_SIM.embedded);
  LIVE_SIM.embedded = null;
}

function initLiveSimPlayer(hostEl, view) {
  const trace = view && view.structured && view.structured.reaction_trace;
  const panel = hostEl.querySelector('.sim-panel');
  if (!trace || !panel) { simStopEmbedded(); return; }
  simStopEmbedded();
  const iconWrap = panel.querySelector('#liveSimIcon');
  const bar = panel.querySelector('#simProgressBar');
  const eventEls = Array.prototype.slice.call(panel.querySelectorAll('.sim-event'));
  const finalEl = panel.querySelector('#simFinal');
  const state = { timer: null, step: 0, total: eventEls.length, iconWrap, bar, eventEls, finalEl };
  LIVE_SIM.embedded = state;

  function reveal(i) {
    if (eventEls[i]) eventEls[i].classList.add('on');
    const pct = state.total ? Math.round(((i + 1) / state.total) * 100) : 100;
    if (bar) bar.style.width = pct + '%';
  }
  function finish() {
    simSetIconRunning(iconWrap, false);
    if (finalEl) { finalEl.hidden = false; finalEl.classList.add('show'); }
  }
  function tick() {
    state.step++;
    if (state.step >= state.total) {
      clearInterval(state.timer); state.timer = null;
      if (state.total) reveal(state.total - 1);
      setTimeout(finish, 500);
      return;
    }
    reveal(state.step);
  }
  state.play = function () {
    simStopEmbeddedVisual(state);
    state.step = 0;
    simSetIconRunning(iconWrap, true);
    reveal(0);
    state.timer = setInterval(tick, 1100);
  };
  const playBtn = panel.querySelector('#simPlayBtn');
  const stopBtn = panel.querySelector('#simStopBtn');
  if (playBtn) playBtn.addEventListener('click', () => state.play());
  if (stopBtn) stopBtn.addEventListener('click', () => simStopEmbedded());
}

/* ---------- 2. 独立「反应模拟器」视图 ---------- */
function simParseReactants(text) {
  return text.split(/[,，、\n]+/).map((p) => p.trim()).filter(Boolean).map((p) => {
    const parts = p.split(/[:：]/);
    const substance = parts[0].trim();
    const moles = parts[1] != null ? parseFloat(parts[1]) : 1;
    return { substance, moles: isNaN(moles) ? 1 : moles };
  }).filter((s) => s.substance);
}
function simParseSolute(text) {
  return text.split(/[,，、\n]+/).map((p) => p.trim()).filter(Boolean).map((p) => {
    const parts = p.split(/[:：]/);
    const substance = parts[0].trim();
    const c = parts[1] != null ? parseFloat(parts[1]) : 0.1;
    return { substance, concentration: isNaN(c) ? 0.1 : c, volume: 1.0 };
  }).filter((s) => s.substance);
}

function renderSimPanel() {
  simStopStandalone();
  const host = document.getElementById('simView');
  host.innerHTML = `
    <div class="live-solve">
      <div class="panel sim-panel">
        <div class="sim-head">
          <div class="sim-icon-wrap" id="standSimIcon">${simFlaskIcon(false, 44)}</div>
          <div class="sim-title">
            <h2 style="margin-top:0">⚗️ 反应模拟器</h2>
            <p class="panel-note" style="margin:2px 0 0">前端模拟器控件：输入物质与条件，调用化学引擎实时模拟；烧瓶图标随模拟运行转动，并与现象/颜色/沉淀动画同步。结果不参与判分，仅供可视化。</p>
          </div>
        </div>
        <div class="sim-quick-row">
          <button class="sim-btn-quick" id="simQuickReasonBtn" type="button">模拟</button>
          <span class="panel-note">一键执行推理计划，烧瓶随状态轨迹转动并同步现象</span>
        </div>
        <p class="panel-note"><label>API 地址：<input id="simApiBase" class="live-input" value="${esc(defaultLiveApiBase())}" spellcheck="false" /></label></p>
        <div class="sim-mode-tabs" id="simModeTabs">
          <button class="sim-mode active" data-mode="trace" type="button">反应轨迹</button>
          <button class="sim-mode" data-mode="equilibrium" type="button">平衡计算</button>
          <button class="sim-mode" data-mode="reason" type="button">多步推理</button>
        </div>
        <div id="simTraceInputs">
          <textarea id="simInput" class="live-textarea" placeholder="反应物，格式 物质:物质的量，逗号分隔，例如：&#10;Fe:1, CuSO4:1&#10;HCl:1, NaOH:1"></textarea>
          <label class="sim-check"><input type="checkbox" id="simAqueous" checked /> 水溶液条件</label>
        </div>
        <div id="simEqInputs" hidden>
          <textarea id="simEqInput" class="live-textarea" placeholder="溶质与浓度，格式 物质:浓度(mol/L)，例如：&#10;CH3COONa:0.1&#10;CH3COOH:0.1"></textarea>
        </div>
        <div id="simReasonInputs" hidden>
          <textarea id="simReasonInitial" class="live-textarea" placeholder="初始体系，格式 物质:物质的量，例如：&#10;AlCl3:1">AlCl3:1</textarea>
          <div class="sim-plan-label">推理计划（按顺序执行：加料 / 切换条件 / 断言；可勾选每步的平衡数值）</div>
          <div class="sim-plan" id="simPlan"></div>
          <button class="sim-btn-mini sim-add-step" id="simAddStep" type="button">＋ 添加步骤</button>
          <label class="sim-check"><input type="checkbox" id="simReasonAqueous" checked /> 水溶液条件</label>
        </div>
        <div class="live-actions">
          <button class="hero-cta" id="simRunBtn" type="button">▶ 开始模拟</button>
          <button class="hero-cta hero-cta-ghost" id="simStopBtn2" type="button">■ 停止</button>
          <span id="simStatus" class="panel-note"></span>
        </div>
      </div>
      <div id="simResult"></div>
    </div>`;
  // 空状态占位：首次进入时提示如何开始（运行后被结果替换）
  const resultHost = host.querySelector('#simResult');
  if (resultHost) resultHost.innerHTML = `
    <div class="panel sim-panel sim-empty">
      <div class="sim-empty-icon">🧪</div>
      <div class="sim-empty-title">开始模拟</div>
      <div class="sim-empty-desc">选择模式并输入物质后点「开始模拟」，或直接点上方「模拟」一键执行多步推理 —— 烧瓶随反应轨迹转动，颜色与沉淀实时同步。</div>
    </div>`;
  const tabs = host.querySelectorAll('.sim-mode');
  tabs.forEach((b) => b.addEventListener('click', () => simSetMode(host, b.dataset.mode)));
  const baseInput = host.querySelector('#simApiBase');
  baseInput.addEventListener('change', () => {
    try { localStorage.setItem('liveApiBase', baseInput.value.trim()); } catch (e) {}
  });
  // 多步推理：计划步骤构建器（默认一个可直接演示的步骤）
  const planHost = host.querySelector('#simPlan');
  simRenderPlan(planHost, [
    { kind: 'AddSpecies', name: '加过量 NaOH', species: [{ substance: 'NaOH', moles: 0.5 }], numerical: true }
  ]);
  host.querySelector('#simAddStep').addEventListener('click', () => {
    planHost.insertAdjacentHTML('beforeend', simPlanRowHtml({ kind: 'AddSpecies' }));
    simBindPlanRows(planHost);
  });
  host.querySelector('#simRunBtn').addEventListener('click', () => simRunStandalone());
  host.querySelector('#simQuickReasonBtn').addEventListener('click', () => simQuickReason());
  host.querySelector('#simStopBtn2').addEventListener('click', () => simStopStandalone());
}

function simStopStandalone() {
  const st = LIVE_SIM.standalone;
  if (st) {
    if (st.timer) { clearInterval(st.timer); st.timer = null; }
    simSetIconRunning(st.iconWrap, false);
    if (st.bar) st.bar.style.width = '0%';
    if (st.eventEls) st.eventEls.forEach((el) => el.classList.remove('on'));
    if (st.finalEl) { st.finalEl.hidden = true; st.finalEl.classList.remove('show'); }
  }
  // 复位多步推理时间线的快照高亮与终态（加性，对其他模式无副作用）
  const res = document.getElementById('simResult');
  if (res) {
    res.querySelectorAll('.sim-snap.show').forEach((el) => el.classList.remove('show'));
    res.querySelectorAll('.sim-snap .sim-event.on').forEach((el) => el.classList.remove('on'));
    const rf = res.querySelector('#reasonFinal');
    if (rf) { rf.hidden = true; rf.classList.remove('show'); }
  }
  LIVE_SIM.standalone = null;
}

// 切换模拟器模式标签（集中管理显示/隐藏，供标签点击与专属「模拟」按钮共用）
function simSetMode(host, mode) {
  host.querySelectorAll('.sim-mode').forEach((x) => x.classList.toggle('active', x.dataset.mode === mode));
  host.querySelector('#simTraceInputs').hidden = mode !== 'trace';
  host.querySelector('#simEqInputs').hidden = mode !== 'equilibrium';
  host.querySelector('#simReasonInputs').hidden = mode !== 'reason';
}
// 从 DOM 收集多步推理请求体（供「开始模拟」reason 分支与专属「模拟」按钮共用）
function simBuildReasonRequest(host) {
  const initText = host.querySelector('#simReasonInitial').value.trim();
  const aqueous = host.querySelector('#simReasonAqueous').checked;
  const initial = simParseReactants(initText);
  const plan = simCollectPlan(host.querySelector('#simPlan'));
  if (!initial.length) return { error: '请输入初始体系，如 AlCl3:1' };
  if (!plan.length) return { error: '请至少添加一个推理步骤' };
  return { req: { initial: { species: initial, conditions: { aqueous } }, plan: { steps: plan } } };
}
// 专属「模拟」按钮：直接执行多步推理并播放动画，不依赖先切模式标签
function simQuickReason() {
  const host = document.getElementById('simView');
  const base = host.querySelector('#simApiBase').value.trim().replace(/\/+$/, '');
  const status = host.querySelector('#simStatus');
  const resultEl = host.querySelector('#simResult');
  simSetMode(host, 'reason'); // 切到多步推理视图，便于看到输入与结果上下文
  simStopStandalone();
  const built = simBuildReasonRequest(host);
  if (built.error) { status.textContent = built.error; return; }
  simRunReason(base, built.req, resultEl, status);
}

function simRunStandalone() {
  const host = document.getElementById('simView');
  const base = host.querySelector('#simApiBase').value.trim().replace(/\/+$/, '');
  const status = host.querySelector('#simStatus');
  const resultEl = host.querySelector('#simResult');
  const mode = host.querySelector('.sim-mode.active').dataset.mode;
  simStopStandalone();
  if (mode === 'trace') {
    const text = host.querySelector('#simInput').value.trim();
    const aqueous = host.querySelector('#simAqueous').checked;
    const species = simParseReactants(text);
    if (!species.length) { status.textContent = '请输入反应物，如 Fe:1, CuSO4:1'; return; }
    simRunTrace(base, species, aqueous, resultEl, status);
  } else if (mode === 'reason') {
    const built = simBuildReasonRequest(host);
    if (built.error) { status.textContent = built.error; return; }
    simRunReason(base, built.req, resultEl, status);
  } else {
    const text = host.querySelector('#simEqInput').value.trim();
    const species = simParseSolute(text);
    if (!species.length) { status.textContent = '请输入溶质与浓度，如 CH3COONa:0.1'; return; }
    simRunEquilibrium(base, species, resultEl, status);
  }
}

async function simRunTrace(base, species, aqueous, resultEl, status) {
  const iconWrap = document.getElementById('standSimIcon');
  const state = { timer: null, iconWrap };
  LIVE_SIM.standalone = state;
  status.textContent = '正在模拟…';
  resultEl.innerHTML = '';
  simSetIconRunning(iconWrap, true);
  try {
    const res = await fetch(base + '/api/chemistry/trace', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ species, conditions: { aqueous } }),
    });
    if (!res.ok) { simStopStandalone(); status.textContent = '请求失败：HTTP ' + res.status; return; }
    const data = await res.json();
    simRenderTraceAnimation(data, resultEl, status, state);
  } catch (err) {
    simStopStandalone();
    status.textContent = '调用失败：' + err.message + '（请确认后端已启动且允许跨域）';
  }
}

function simRenderTraceAnimation(data, resultEl, status, state) {
  const initial = data.initialSpecies || [];
  const events = data.events || [];
  const fin = data.finalState || {};
  const finSpecies = fin.species || [];
  const chip = (sp) => `<span class="sim-chip">${simDot(sp.color)}${chem(sp.formula || sp.name || sp.id || '')}${sp.initialMoles != null ? '：' + sp.initialMoles + ' mol' : ''}<span class="panel-note">${esc(sp.amountText || '')}</span></span>`;
  const evHtml = events.map((ev, i) => {
    const consumed = Object.entries(ev.consumed || {}).map(([k, v]) => `${chem(k)} -${v}`).join('，');
    const produced = Object.entries(ev.produced || {}).map(([k, v]) => `${chem(k)} +${v}`).join('，');
    const phen = (ev.phenomena || []).map((p) => `<span class="sim-chip sim-chip-soft">${esc(p)}</span>`).join('');
    const tm = simEventTypeMeta(ev.type);
    return `<li class="sim-event" data-i="${i}" style="--etc:${tm.color}">` +
      `<div class="sim-event-eq"><span class="sim-step-no">${i + 1}</span>${chem(ev.equation || '')}</div>` +
      `<div class="sim-event-meta"><span class="sim-type-tag" style="background:${tm.color}1a;color:${tm.color};border-color:${tm.color}33">${esc(tm.label)}</span>${esc(ev.type || '')} · 进度 ${fmtExtent(ev.extent)}</div>` +
      (consumed ? `<div class="sim-delta">消耗：<span class="mono">${consumed}</span></div>` : '') +
      (produced ? `<div class="sim-delta">生成：<span class="mono">${produced}</span></div>` : '') +
      (phen ? `<div class="sim-phen">${phen}</div>` : '') +
      (ev.solutionColorAfter ? `<div class="sim-color-line">溶液颜色 → ${simDot(ev.solutionColorAfter)}${esc(ev.solutionColorAfter)}</div>` : '') +
    `</li>`;
  }).join('');
  const finChips = finSpecies.map((sp) =>
    `<span class="sim-chip">${simDot(sp.color)}${chem(sp.formula || sp.name || '')}${sp.finalMoles != null ? '：' + sp.finalMoles + ' mol' : ''}<span class="panel-note">${esc(sp.amountText || '')}</span></span>`
  ).join('');
  resultEl.innerHTML = `<div class="panel sim-panel">` +
    `<div class="sim-progress"><div class="sim-progress-bar" id="standProg"></div></div>` +
    `<div class="sim-phase"><b>初始态</b><div class="sim-chips" style="margin-top:4px">${initial.map(chip).join('') || '<span class="panel-note">无</span>'}</div></div>` +
    `<ol class="sim-events" id="standEvents">${evHtml}</ol>` +
    `<div class="sim-final" id="standFinal" hidden>` +
      `<div class="sim-final-label">终态</div>` +
      (fin.solutionColor ? `<div class="sim-color-line">溶液颜色：${simDot(fin.solutionColor)}${esc(fin.solutionColor)}</div>` : '') +
      `<div class="sim-chips">${finChips || '<span class="panel-note">无</span>'}</div>` +
      ((fin.precipitates && fin.precipitates.length) ? `<div class="sim-phen">${fin.precipitates.map((p) => `<span class="sim-chip sim-chip-soft">沉淀：${esc(p)}</span>`).join('')}</div>` : '') +
      (fin.summary ? `<div class="sim-summary">${esc(fin.summary)}</div>` : '') +
    `</div>` +
    ((data.warnings && data.warnings.length) ? `<ul class="panel-note">${data.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>` : '') +
  `</div>`;

  const eventEls = Array.prototype.slice.call(resultEl.querySelectorAll('.sim-event'));
  const bar = resultEl.querySelector('#standProg');
  const finalEl = resultEl.querySelector('#standFinal');
  const total = eventEls.length;
  state.step = 0; state.total = total; state.eventEls = eventEls; state.bar = bar; state.finalEl = finalEl;

  function reveal(i) {
    if (eventEls[i]) eventEls[i].classList.add('on');
    if (bar) bar.style.width = (total ? Math.round(((i + 1) / total) * 100) : 100) + '%';
  }
  function finish() {
    simSetIconRunning(state.iconWrap, false);
    if (finalEl) { finalEl.hidden = false; finalEl.classList.add('show'); }
    status.textContent = '模拟完成（收敛：' + (data.converged ? '是' : '否') + '）';
  }
  function tick() {
    state.step++;
    if (state.step >= total) {
      clearInterval(state.timer); state.timer = null;
      if (total) reveal(total - 1);
      setTimeout(finish, 550);
      return;
    }
    reveal(state.step);
  }
  if (total === 0) { finish(); return; }
  reveal(0);
  state.timer = setInterval(tick, 1300);
}

async function simRunEquilibrium(base, species, resultEl, status) {
  const iconWrap = document.getElementById('standSimIcon');
  const state = { iconWrap };
  LIVE_SIM.standalone = state;
  status.textContent = '正在计算…';
  resultEl.innerHTML = '';
  simSetIconRunning(iconWrap, true);
  try {
    const res = await fetch(base + '/api/chemistry/equilibrium', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ input: { species }, query: { quantities: ['Ph', 'IonConcentrations', 'ConcentrationOrdering', 'Conservations', 'AcidityQualitative'] } }),
    });
    simSetIconRunning(iconWrap, false);
    if (!res.ok) { status.textContent = '请求失败：HTTP ' + res.status; return; }
    const d = await res.json();
    let inner = `<div class="eq-block"><ul>`;
    inner += `<li>状态：<b>${esc(d.status || '')}</b></li>`;
    if (d.ph != null) inner += `<li>pH ≈ <b>${d.ph}</b></li>`;
    if (d.acidityVerdict) inner += `<li>酸碱性：${esc(d.acidityVerdict)}</li>`;
    if (d.concentrationOrdering && d.concentrationOrdering.length) inner += `<li>浓度排序：<span class="mono">${d.concentrationOrdering.map(chem).join(' > ')}</span></li>`;
    if (d.conservationEquations && d.conservationEquations.length) inner += `<li>守恒式：<ul>${d.conservationEquations.map((e) => `<li class="mono">${chem(e)}</li>`).join('')}</ul></li>`;
    if (d.ionConcentrations && Object.keys(d.ionConcentrations).length) inner += `<li>离子浓度：<ul>${Object.entries(d.ionConcentrations).map(([k, v]) => `<li class="mono">${chem(k)} = ${v}</li>`).join('')}</ul></li>`;
    if (d.conversionConstantK) inner += `<li>沉淀转化常数 K：<span class="mono">${esc(d.conversionConstantK)}</span>（自发：${d.conversionProceeds ? '是' : '否'}）</li>`;
    inner += `</ul></div>`;
    if (d.derivationSteps && d.derivationSteps.length) inner += `<h5>推导</h5><ol class="steps">${d.derivationSteps.map((x) => `<li><div class="step-body">${esc(x)}</div></li>`).join('')}</ol>`;
    if (d.warnings && d.warnings.length) inner += `<ul class="panel-note">${d.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>`;
    resultEl.innerHTML = `<div class="panel sim-panel">${inner}</div>`;
    status.textContent = '计算完成（' + (d.status || '') + '）';
  } catch (err) {
    simSetIconRunning(iconWrap, false);
    status.textContent = '调用失败：' + err.message + '（请确认后端已启动且允许跨域）';
  }
}

/* ---------- 3. 多步推理模式（/reason，状态轨迹逐步展开动画） ---------- */

// 单行推理步骤的 HTML（加料 / 切换条件 / 断言）
function simPlanRowHtml(step) {
  step = step || {};
  const kind = step.kind || 'AddSpecies';
  const name = step.name || '';
  const spec = (step.species || []).map((s) => `${s.substance}:${s.moles}`).join(', ');
  const num = step.numerical ? 'checked' : '';
  return `<div class="sim-step-row">` +
    `<select class="sim-step-kind" title="步骤类型">` +
      `<option value="AddSpecies"${kind === 'AddSpecies' ? ' selected' : ''}>加料</option>` +
      `<option value="SetConditions"${kind === 'SetConditions' ? ' selected' : ''}>切换条件</option>` +
      `<option value="Assert"${kind === 'Assert' ? ' selected' : ''}>断言</option>` +
    `</select>` +
    `<input class="sim-step-name" type="text" placeholder="步骤名，如 加过量 NaOH" value="${esc(name)}" />` +
    `<input class="sim-step-spec" type="text" placeholder="物质:物质的量，如 NaOH:0.5" value="${esc(spec)}" />` +
    `<label class="sim-check"><input type="checkbox" class="sim-step-num" ${num} /> 查平衡数值</label>` +
    `<button class="sim-btn-mini sim-rm-step" type="button" title="删除此步">✕</button>` +
  `</div>`;
}
function simBindPlanRows(host) {
  host.querySelectorAll('.sim-rm-step').forEach((btn) => {
    btn.onclick = () => { const row = btn.closest('.sim-step-row'); if (row) row.remove(); };
  });
}
function simRenderPlan(host, plan) {
  host.innerHTML = '';
  (plan || []).forEach((s) => { host.insertAdjacentHTML('beforeend', simPlanRowHtml(s)); });
  simBindPlanRows(host);
}
// 从 DOM 收集推理计划 -> ReasoningPlan.steps
function simCollectPlan(host) {
  const rows = host.querySelectorAll('.sim-step-row');
  const steps = [];
  rows.forEach((row) => {
    const kind = row.querySelector('.sim-step-kind').value;
    const name = row.querySelector('.sim-step-name').value.trim();
    const specText = row.querySelector('.sim-step-spec').value.trim();
    const num = row.querySelector('.sim-step-num').checked;
    const step = { kind, name: name || ('步骤 ' + (steps.length + 1)) };
    if (kind === 'AddSpecies') {
      const species = simParseReactants(specText);
      if (species.length) step.species = species;
    }
    if (num) step.numerical = { quantities: ['Ph', 'IonConcentrations', 'ConcentrationOrdering', 'Conservations', 'AcidityQualitative'] };
    steps.push(step);
  });
  return steps;
}

async function simRunReason(base, req, resultEl, status) {
  const iconWrap = document.getElementById('standSimIcon');
  const state = { timer: null, iconWrap };
  LIVE_SIM.standalone = state;
  status.textContent = '正在推理…';
  resultEl.innerHTML = '';
  simSetIconRunning(iconWrap, true);
  try {
    const res = await fetch(base + '/api/chemistry/reason', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    if (!res.ok) { simStopStandalone(); status.textContent = '请求失败：HTTP ' + res.status; return; }
    const data = await res.json();
    simRenderReasonAnimation(data, resultEl, status, state);
  } catch (err) {
    simStopStandalone();
    status.textContent = '调用失败：' + err.message + '（请确认后端已启动且允许跨域）';
  }
}

// 单个物种 chip（带色点 + 化学式 + 量）
function simSpeciesChip(sp) {
  return `<span class="sim-chip">${simDot(sp.color)}${chem(sp.formula || sp.name || sp.id || '')}${sp.finalMoles != null ? '：' + sp.finalMoles + ' mol' : ''}<span class="panel-note">${esc(sp.amountText || '')}</span></span>`;
}

// 反应类型 → 配色与中文标签（仅用于事件卡左侧色条与类型标签，纯展示，不影响逻辑）
const SIM_EVENT_TYPES = {
  Precipitation:   { color: '#dc2626', label: '沉淀' },
  GasEvolution:    { color: '#ea580c', label: '气体' },
  Neutralization:  { color: '#db2777', label: '中和' },
  Displacement:    { color: '#2563eb', label: '置换' },
  Redox:           { color: '#4f46e5', label: '氧化还原' },
  Oxidation:       { color: '#7c3aed', label: '氧化' },
  Complexation:    { color: '#0891b2', label: '络合' },
  Metathesis:      { color: '#0d9488', label: '复分解' },
  Combination:     { color: '#16a34a', label: '化合' },
  Decomposition:   { color: '#d97706', label: '分解' },
  Hydrolysis:      { color: '#475569', label: '水解' },
  WeakElectrolyte: { color: '#be185d', label: '弱电解质' },
  Esterification:  { color: '#9333ea', label: '酯化' },
  Addition:        { color: '#0284c7', label: '加成' },
  Substitution:    { color: '#059669', label: '取代' },
  NoReaction:      { color: '#94a3b8', label: '无反应' },
};
function simEventTypeMeta(type) {
  return (type && SIM_EVENT_TYPES[type]) || { color: '#64748b', label: type || '' };
}
function fmtExtent(v) {
  return typeof v === 'number' ? v.toFixed(3).replace(/\.?0+$/, '') : (v ?? '-');
}

// 反应事件 HTML（与 trace 模式同形，复用 .sim-event 样式与展开动画）
function simEventHtml(ev, i) {
  const consumed = Object.entries(ev.consumed || {}).map(([k, v]) => `${chem(k)} -${v}`).join('，');
  const produced = Object.entries(ev.produced || {}).map(([k, v]) => `${chem(k)} +${v}`).join('，');
  const phen = (ev.phenomena || []).map((p) => `<span class="sim-chip sim-chip-soft">${esc(p)}</span>`).join('');
  const tm = simEventTypeMeta(ev.type);
  return `<li class="sim-event" data-i="${i}" style="--etc:${tm.color}">` +
    `<div class="sim-event-eq"><span class="sim-step-no">${i + 1}</span>${chem(ev.equation || '')}</div>` +
    `<div class="sim-event-meta"><span class="sim-type-tag" style="background:${tm.color}1a;color:${tm.color};border-color:${tm.color}33">${esc(tm.label)}</span>${esc(ev.type || '')} · 进度 ${fmtExtent(ev.extent)}</div>` +
    (consumed ? `<div class="sim-delta">消耗：<span class="mono">${consumed}</span></div>` : '') +
    (produced ? `<div class="sim-delta">生成：<span class="mono">${produced}</span></div>` : '') +
    (phen ? `<div class="sim-phen">${phen}</div>` : '') +
    (ev.solutionColorAfter ? `<div class="sim-color-line">溶液颜色 → ${simDot(ev.solutionColorAfter)}${esc(ev.solutionColorAfter)}</div>` : '') +
  `</li>`;
}

// 平衡数值块（mirror simRunEquilibrium 的渲染，嵌入到每个快照）
function simNumericalHtml(d) {
  if (!d) return '';
  let inner = `<div class="sim-numerical"><ul>`;
  inner += `<li>状态：<b>${esc(d.status || '')}</b></li>`;
  if (d.ph != null) inner += `<li>pH ≈ <b>${d.ph}</b></li>`;
  if (d.acidityVerdict) inner += `<li>酸碱性：${esc(d.acidityVerdict)}</li>`;
  if (d.concentrationOrdering && d.concentrationOrdering.length) inner += `<li>浓度排序：<span class="mono">${d.concentrationOrdering.map(chem).join(' > ')}</span></li>`;
  if (d.conservationEquations && d.conservationEquations.length) inner += `<li>守恒式：<ul>${d.conservationEquations.map((e) => `<li class="mono">${chem(e)}</li>`).join('')}</ul></li>`;
  if (d.ionConcentrations && Object.keys(d.ionConcentrations).length) inner += `<li>离子浓度：<ul>${Object.entries(d.ionConcentrations).map(([k, v]) => `<li class="mono">${chem(k)} = ${v}</li>`).join('')}</ul></li>`;
  if (d.conversionConstantK) inner += `<li>沉淀转化常数 K：<span class="mono">${esc(d.conversionConstantK)}</span>（自发：${d.conversionProceeds ? '是' : '否'}）</li>`;
  inner += `</ul>`;
  if (d.warnings && d.warnings.length) inner += `<ul class="panel-note">${d.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>`;
  inner += `</div>`;
  return inner;
}

// 单个状态快照卡片 HTML
function simSnapHtml(snap, idx) {
  const st = snap.state || {};
  const species = st.species || [];
  const events = snap.events || [];
  const chips = species.map(simSpeciesChip).join('');
  const evHtml = events.map(simEventHtml).join('');
  const stage = esc(snap.stageName || ('步骤 ' + idx));
  return `<div class="sim-snap" data-step="${idx}">` +
    `<div class="sim-snap-head"><span class="sim-step-badge">${idx}</span>${stage}</div>` +
    `<div class="sim-snap-label">体系状态</div><div class="sim-chips">${chips || '<span class="panel-note">无</span>'}</div>` +
    (st.solutionColor ? `<div class="sim-color-line">溶液颜色：${simDot(st.solutionColor)}${esc(st.solutionColor)}</div>` : '') +
    ((st.precipitates && st.precipitates.length) ? `<div class="sim-phen">${st.precipitates.map((p) => `<span class="sim-chip sim-chip-soft">沉淀：${esc(p)}</span>`).join('')}</div>` : '') +
    (events.length ? `<div class="sim-snap-label">反应事件</div><ol class="sim-events">${evHtml}</ol>` : '') +
    (snap.numerical ? `<div class="sim-snap-label">该步平衡数值</div>${simNumericalHtml(snap.numerical)}` : '') +
  `</div>`;
}

// 状态轨迹逐步展开：与烧瓶图标动画同步
function simRenderReasonAnimation(data, resultEl, status, state) {
  if (LIVE_SIM.standalone !== state) return; // 期间用户已停止，放弃渲染
  const traj = data.trajectory || [];
  const fin = data.finalState || {};
  const snapsHtml = traj.map((s, i) => simSnapHtml(s, i)).join('');
  const finChips = (fin.species || []).map(simSpeciesChip).join('');
  resultEl.innerHTML = `<div class="panel sim-panel">` +
    `<div class="sim-progress"><div class="sim-progress-bar" id="reasonProg"></div></div>` +
    `<div class="sim-timeline" id="reasonTimeline">${snapsHtml}</div>` +
    `<div class="sim-final" id="reasonFinal" hidden>` +
      `<div class="sim-final-label">终态</div>` +
      (fin.solutionColor ? `<div class="sim-color-line">溶液颜色：${simDot(fin.solutionColor)}${esc(fin.solutionColor)}</div>` : '') +
      `<div class="sim-chips">${finChips || '<span class="panel-note">无</span>'}</div>` +
      ((fin.precipitates && fin.precipitates.length) ? `<div class="sim-phen">${fin.precipitates.map((p) => `<span class="sim-chip sim-chip-soft">沉淀：${esc(p)}</span>`).join('')}</div>` : '') +
      (fin.summary ? `<div class="sim-summary">${esc(fin.summary)}</div>` : '') +
    `</div>` +
    ((data.evidence && data.evidence.length) ? `<ul class="panel-note sim-evidence"><b>依据：</b>${data.evidence.map((e) => `<li>${esc(e)}</li>`).join('')}</ul>` : '') +
    ((data.warnings && data.warnings.length) ? `<ul class="panel-note">${data.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>` : '') +
  `</div>`;

  const snapEls = Array.prototype.slice.call(resultEl.querySelectorAll('.sim-snap'));
  const bar = resultEl.querySelector('#reasonProg');
  const finalEl = resultEl.querySelector('#reasonFinal');
  const total = snapEls.length;
  state.step = 0; state.total = total; state.snapEls = snapEls; state.bar = bar; state.finalEl = finalEl;

  function revealSnap(i) {
    const el = snapEls[i];
    if (el) {
      el.classList.add('show');
      // 快照内的事件错峰展开（视觉更顺滑）
      const evs = el.querySelectorAll('.sim-event');
      evs.forEach((evEl, k) => { setTimeout(() => { if (LIVE_SIM.standalone === state) evEl.classList.add('on'); }, 130 * k + 90); });
    }
    if (bar) bar.style.width = (total ? Math.round(((i + 1) / total) * 100) : 100) + '%';
  }
  function finish() {
    if (LIVE_SIM.standalone !== state) return;
    simSetIconRunning(state.iconWrap, false);
    if (finalEl) { finalEl.hidden = false; finalEl.classList.add('show'); }
    status.textContent = '推理完成，共 ' + total + ' 个状态快照';
  }
  function tick() {
    state.step++;
    if (state.step >= total) {
      clearInterval(state.timer); state.timer = null;
      if (total) revealSnap(total - 1);
      setTimeout(finish, 650);
      return;
    }
    revealSnap(state.step);
  }
  simSetIconRunning(state.iconWrap, true);
  if (total === 0) { finish(); return; }
  revealSnap(0);
  state.timer = setInterval(tick, 1500);
}
