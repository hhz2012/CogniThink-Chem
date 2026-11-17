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
let activeCategory = 'All';
let keyword = '';

init();

async function init() {
  // 仅在主应用页面（index.html）执行完整初始化；演示页等跳过
  if (!document.getElementById('loadingTip')) return;

  try {
    const res = await fetch('data/problems.json');
    if (!res.ok) throw new Error('HTTP ' + res.status);
    DATA = await res.json();
  } catch (err) {
    showError('加载 data/problems.json 失败（' + err.message +
      '）。请通过 HTTP 服务访问本页（如在仓库根目录运行 python -m http.server 后打开 /web/），或直接访问 GitHub Pages。');
    return;
  }

  document.getElementById('loadingTip').hidden = true;
  renderStats();
  renderCatTabs();
  renderFooter();

  document.getElementById('searchBox').addEventListener('input', (e) => {
    keyword = e.target.value.trim().toLowerCase();
    renderList();
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

/* ---------- 列表视图 ---------- */

function route() {
  if (!DATA) return;
  const id = decodeURIComponent(location.hash.replace(/^#\/?/, ''));
  const rec = id ? DATA.problems.find((r) => r.problem.id === id) : null;
  if (rec) {
    showDetail(rec);
  } else {
    showList();
  }
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

function stepHtml(st) {
  const chips = [];
  if (st.evidence) chips.push(`<span class="chip mono">${esc(st.evidence)}</span>`);
  if (st.knowledgeId) chips.push(`<span class="chip chip-k mono">${esc(st.knowledgeId)}</span>`);
  if (st.source) chips.push(`<span class="chip chip-src">${esc(st.source)}</span>`);
  return `
    <li>
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

/* 1. 电化学装置图（SVG）：原电池/电解池，标注电极、电子流向、离子移向 */
function electrochemDiagramHtml(cell, electrodes) {
  if (!cell) return '';
  const galvanic = cell.type === 'galvanic';
  const oxLabel = galvanic ? '负极' : '阳极';
  const redLabel = galvanic ? '正极' : '阴极';
  const oxEq = galvanic ? electrodes?.negative : electrodes?.anode;
  const redEq = galvanic ? electrodes?.positive : electrodes?.cathode;
  const oxMat = cell.electrodes?.[0] || '电极';
  const redMat = cell.electrodes?.[1] || '电极';
  const electrolyte = cell.electrolyte || '电解质溶液';
  const medium = cell.medium === 'acidic' ? '酸性' : cell.medium === 'basic' ? '碱性' : '';

  return `
  <div class="viz-block">
    <h5>🔋 装置示意图</h5>
    <div class="elec-diagram">
      <svg viewBox="0 0 640 260" class="elec-svg" xmlns="http://www.w3.org/2000/svg">
        <!-- 外电路导线 -->
        <line x1="140" y1="70" x2="500" y2="70" stroke="#475569" stroke-width="3"/>
        <!-- 电子流向箭头（外电路） -->
        <g class="elec-electron-flow">
          <line x1="200" y1="70" x2="440" y2="70" stroke="#f59e0b" stroke-width="2" stroke-dasharray="6 4" marker-end="url(#arrowGold)"/>
          <text x="320" y="58" text-anchor="middle" fill="#d97706" font-size="12" font-weight="600">e⁻ 外电路流向</text>
        </g>
        <defs>
          <marker id="arrowGold" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
            <path d="M0,0 L8,4 L0,8 Z" fill="#f59e0b"/>
          </marker>
          <marker id="arrowBlue" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
            <path d="M0,0 L8,4 L0,8 Z" fill="#3b82f6"/>
          </marker>
          <marker id="arrowGreen" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
            <path d="M0,0 L8,4 L0,8 Z" fill="#10b981"/>
          </marker>
        </defs>

        <!-- 左电极 -->
        <rect x="100" y="70" width="80" height="150" rx="8" fill="#fef3c7" stroke="#d97706" stroke-width="2"/>
        <text x="140" y="55" text-anchor="middle" font-size="13" font-weight="700" fill="#92400e">${esc(oxLabel)}</text>
        <text x="140" y="100" text-anchor="middle" font-size="12" fill="#78350f">${esc(oxMat)}</text>
        <text x="140" y="120" text-anchor="middle" font-size="11" fill="#92400e">（氧化·失电子）</text>
        ${oxEq ? `<text x="140" y="155" text-anchor="middle" font-size="10" fill="#78350f">${chem(oxEq).replace(/<[^>]+>/g, '')}</text>` : ''}

        <!-- 右电极 -->
        <rect x="460" y="70" width="80" height="150" rx="8" fill="#dbeafe" stroke="#2563eb" stroke-width="2"/>
        <text x="500" y="55" text-anchor="middle" font-size="13" font-weight="700" fill="#1e3a8a">${esc(redLabel)}</text>
        <text x="500" y="100" text-anchor="middle" font-size="12" fill="#1e40af">${esc(redMat)}</text>
        <text x="500" y="120" text-anchor="middle" font-size="11" fill="#1e3a8a">（还原·得电子）</text>
        ${redEq ? `<text x="500" y="155" text-anchor="middle" font-size="10" fill="#1e40af">${chem(redEq).replace(/<[^>]+>/g, '')}</text>` : ''}

        <!-- 电解质溶液（中间容器） -->
        <rect x="180" y="130" width="280" height="90" rx="6" fill="#f0fdfa" stroke="#0d9488" stroke-width="2"/>
        <text x="320" y="155" text-anchor="middle" font-size="13" font-weight="600" fill="#115e59">${esc(electrolyte)}${medium ? '（' + medium + '）' : ''}</text>

        <!-- 离子移向（内电路） -->
        <text x="250" y="190" text-anchor="middle" font-size="11" fill="#2563eb">阳离子 →</text>
        <text x="390" y="190" text-anchor="middle" font-size="11" fill="#dc2626">← 阴离子</text>
        <line x1="210" y1="200" x2="290" y2="200" stroke="#3b82f6" stroke-width="2" marker-end="url(#arrowBlue)"/>
        <line x1="430" y1="200" x2="350" y2="200" stroke="#dc2626" stroke-width="2" marker-end="url(#arrowGreen)"/>

        ${galvanic ? '' : `
          <!-- 电源符号（电解池） -->
          <rect x="295" y="20" width="50" height="24" rx="4" fill="#1f2937"/>
          <text x="320" y="37" text-anchor="middle" font-size="13" font-weight="700" fill="#fbbf24">DC</text>
          <text x="295" y="46" text-anchor="middle" font-size="11" fill="#dc2626">+</text>
          <text x="345" y="46" text-anchor="middle" font-size="11" fill="#2563eb">−</text>
          <line x1="305" y1="44" x2="140" y2="70" stroke="#dc2626" stroke-width="2"/>
          <line x1="335" y1="44" x2="500" y2="70" stroke="#2563eb" stroke-width="2"/>
        `}
      </svg>
      <p class="viz-caption">${galvanic ? '原电池：负极氧化失电子，正极还原得电子；电子经外电路由负极流向正极，阳离子向正极移动。' : '电解池：阳极（接电源+）氧化，阴极（接电源−）还原；电子由阳极经外电路流向阴极。'}</p>
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

/* 3. 粒子浓度定性柱状图 */
function concentrationBarsHtml(order) {
  if (!order || !order.length) return '';
  const n = order.length;
  const maxH = 140;
  const bars = order.map((sp, i) => {
    const h = Math.max(20, maxH - i * (maxH / (n + 1)));
    return `
      <div class="bar-col">
        <div class="bar-label">${chem(sp)}</div>
        <div class="bar-value"></div>
        <div class="bar" style="height:${h}px"></div>
      </div>`;
  }).join('');

  return `
  <div class="viz-block">
    <h5>📊 粒子浓度相对大小</h5>
    <div class="bar-chart">
      ${bars}
    </div>
    <p class="viz-caption">注：柱高为相对示意（由浓度由大到小排序得出），不代表真实浓度比。</p>
  </div>`;
}

/* 4. 离子共存冲突图 */
function coexistenceGraphHtml(conflicts, species, medium) {
  if (!species || !species.length) return '';
  const allIons = [...species];
  if (medium === 'acidic' && !allIons.includes('H^+')) allIons.push('H^+');
  if (medium === 'basic' && !allIons.includes('OH^-')) allIons.push('OH^-');

  const conflictColor = {
    '生成沉淀': '#dc2626',
    '生成气体': '#ea580c',
    '发生氧化还原反应': '#7c3aed',
    '相互促进水解（双水解）': '#0891b2',
    '生成弱电解质': '#ca8a04',
  };

  if (!conflicts || !conflicts.length) {
    return `
    <div class="viz-block">
      <h5>🔗 离子共存关系图</h5>
      <div class="coex-graph coex-ok">
        <div class="coex-nodes">
          ${allIons.map((ion) => `<span class="coex-node coex-ok-node">${chem(ion)}</span>`).join('')}
        </div>
        <p class="coex-verdict coex-ok-text">✓ 课内知识范围内无冲突，能大量共存</p>
      </div>
    </div>`;
  }

  // 邻接表：记录哪些离子对有冲突
  const conflictPairs = new Set();
  const ionReasons = {};
  conflicts.forEach((c) => {
    const ions = c.ions || [];
    for (let i = 0; i < ions.length; i++) {
      for (let j = i + 1; j < ions.length; j++) {
        const pair = [ions[i], ions[j]].sort().join('|');
        conflictPairs.add(pair);
      }
    }
    ions.forEach((ion) => {
      if (!ionReasons[ion]) ionReasons[ion] = [];
      if (c.reason && !ionReasons[ion].includes(c.reason)) ionReasons[ion].push(c.reason);
    });
  });

  const nodes = allIons.map((ion) => {
    const hasConflict = conflictPairs.size > 0 && Array.from(conflictPairs).some((p) => p.includes(ion));
    const reasons = ionReasons[ion] || [];
    return `
      <div class="coex-node-wrap" title="${reasons.join('；')}">
        <span class="coex-node ${hasConflict ? 'coex-conflict' : 'coex-safe'}">${chem(ion)}</span>
        ${hasConflict && reasons.length ? `<div class="coex-node-reason">${reasons.map(esc).join('<br>')}</div>` : ''}
      </div>`;
  }).join('');

  return `
  <div class="viz-block">
    <h5>🔗 离子共存关系图</h5>
    <div class="coex-graph">
      <div class="coex-nodes">${nodes}</div>
      <div class="coex-legend">
        <span><span class="legend-dot" style="background:#dc2626"></span>生成沉淀</span>
        <span><span class="legend-dot" style="background:#ea580c"></span>生成气体</span>
        <span><span class="legend-dot" style="background:#7c3aed"></span>氧化还原</span>
        <span><span class="legend-dot" style="background:#0891b2"></span>双水解</span>
        <span><span class="legend-dot" style="background:#ca8a04"></span>弱电解质</span>
      </div>
    </div>
  </div>`;
}

/* 5. 摩尔计算关系图 */
function moleRelationHtml(numericValue, numericUnit, numericSpecies) {
  const highlight = {};
  if (numericUnit) {
    const u = numericUnit;
    if (u.includes('g')) highlight.mass = true;
    if (u.includes('mol')) highlight.mole = true;
    if (u.includes('L')) highlight.gas = true;
    if (u.includes('/L') || u.includes('mol/L')) highlight.conc = true;
  }

  const node = (key, label, formula, isHighlight) => `
    <div class="mole-node ${isHighlight ? 'mole-hl' : ''}">
      <div class="mole-node-label">${esc(label)}</div>
      <div class="mole-node-formula mono">${esc(formula)}</div>
    </div>`;

  return `
  <div class="viz-block">
    <h5>🧮 物质的量换算关系</h5>
    <div class="mole-diagram">
      <div class="mole-center">
        ${node('n', '物质的量 n', numericValue ? `${esc(numericValue)} ${esc(numericUnit || 'mol')}` : 'n = ?', true)}
      </div>
      <div class="mole-ring">
        ${node('m', '质量 m', 'm = n·M', !!highlight.mass)}
        ${node('N', '粒子数 N', 'N = n·Nₐ', false)}
        ${node('V', '气体体积 V', 'V = n·Vₘ', !!highlight.gas)}
        ${node('c', '浓度 c', 'c = n/V', !!highlight.conc)}
      </div>
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

  const W = 520, H = 280;
  const padL = 50, padR = 20, padT = 20, padB = 40;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;

  // 找最大值作为 y 轴上限
  const maxVal = Math.max(...series.flatMap((s) => [s.start, s.end]), 0.1);
  const yMax = Math.ceil(maxVal * 1.1 * 10) / 10;

  // 时间轴：0 → t_eq（达到平衡）；若有扰动，在 60% 处标记
  const hasDisturbance = !!equilibrium?.direction;
  const tEq = 100;
  const tDisturb = hasDisturbance ? 60 : -1;

  // 生成平滑曲线点（每物种 20 个点，指数趋近平衡）
  const palette = ['#2563eb', '#dc2626', '#16a34a', '#d97706', '#7c3aed', '#0891b2'];
  const pts = series.map((s, idx) => {
    const color = palette[idx % palette.length];
    const points = [];
    for (let t = 0; t <= tEq; t += 5) {
      const frac = t / tEq;
      // 指数趋近：c(t) = end + (start - end) * exp(-k*t)，k 使 t=tEq 时接近 99%
      const k = -Math.log(0.01) / tEq;
      const c = s.end + (s.start - s.end) * Math.exp(-k * t);
      const x = padL + (frac * plotW);
      const y = padT + plotH - (c / yMax) * plotH;
      points.push(`${x.toFixed(1)},${y.toFixed(1)}`);
    }
    return { species: s.species, color, points: points.join(' ') };
  });

  // 坐标轴
  const yTicks = 5;
  const yTickLines = Array.from({ length: yTicks + 1 }, (_, i) => {
    const v = (yMax / yTicks) * i;
    const y = padT + plotH - (v / yMax) * plotH;
    return `<line x1="${padL}" y1="${y}" x2="${padL + plotW}" y2="${y}" stroke="#e2e8f0" stroke-width="1"/>
            <text x="${padL - 8}" y="${y + 4}" text-anchor="end" font-size="10" fill="#94a3b8">${v.toFixed(1)}</text>`;
  }).join('');

  const xLabels = `<text x="${padL}" y="${H - padB + 20}" text-anchor="middle" font-size="11" fill="#64748b">0</text>
    <text x="${padL + plotW}" y="${H - padB + 20}" text-anchor="middle" font-size="11" fill="#64748b">t<tspan baseline-shift="sub" font-size="9">eq</tspan></text>
    <text x="${padL + plotW / 2}" y="${H - 6}" text-anchor="middle" font-size="12" fill="#475569">时间 t</text>
    <text x="14" y="${padT + plotH / 2}" text-anchor="middle" font-size="12" fill="#475569" transform="rotate(-90 14 ${padT + plotH / 2})">浓度 c</text>`;

  const disturbLine = hasDisturbance ? `
    <line x1="${padL + (tDisturb / tEq) * plotW}" y1="${padT}" x2="${padL + (tDisturb / tEq) * plotW}" y2="${padT + plotH}" stroke="#ef4444" stroke-width="1.5" stroke-dasharray="5 4"/>
    <text x="${padL + (tDisturb / tEq) * plotW}" y="${padT - 6}" text-anchor="middle" font-size="10" fill="#ef4444" font-weight="600">扰动</text>
  ` : '';

  const legend = pts.map((p) => `
    <span class="ct-legend-item">
      <span class="ct-legend-dot" style="background:${p.color}"></span>${chem(p.species)}
    </span>
  `).join('');

  const paths = pts.map((p) => `
    <polyline points="${p.points}" fill="none" stroke="${p.color}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
  `).join('');

  return `
  <div class="viz-block">
    <h5>📈 浓度-时间示意图</h5>
    <div class="ct-chart">
      <svg viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg">
        ${yTickLines}
        <line x1="${padL}" y1="${padT}" x2="${padL}" y2="${padT + plotH}" stroke="#475569" stroke-width="1.5"/>
        <line x1="${padL}" y1="${padT + plotH}" x2="${padL + plotW}" y2="${padT + plotH}" stroke="#475569" stroke-width="1.5"/>
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
      <svg viewBox="0 0 360 220" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="60" y1="20" x2="60" y2="200" stroke="#475569" stroke-width="3"/>
        <line x1="30" y1="200" x2="90" y2="200" stroke="#475569" stroke-width="4"/>
        <!-- 蒸馏烧瓶 -->
        <ellipse cx="150" cy="130" rx="45" ry="50" fill="#dbeafe" stroke="#2563eb" stroke-width="2"/>
        <rect x="138" y="70" width="24" height="30" fill="#dbeafe" stroke="#2563eb" stroke-width="2"/>
        <!-- 温度计 -->
        <line x1="150" y1="40" x2="150" y2="85" stroke="#dc2626" stroke-width="2"/>
        <circle cx="150" cy="40" r="5" fill="#dc2626"/>
        <text x="170" y="50" font-size="10" fill="#475569">温度计</text>
        <text x="170" y="62" font-size="9" fill="#92400e">水银球在支管口</text>
        <!-- 支管口 -->
        <line x1="195" y1="105" x2="240" y2="105" stroke="#2563eb" stroke-width="2"/>
        <!-- 冷凝管 -->
        <rect x="240" y="80" width="80" height="50" rx="6" fill="#f0fdfa" stroke="#0d9488" stroke-width="2"/>
        <line x1="240" y1="105" x2="320" y2="105" stroke="#0d9488" stroke-width="1.5" stroke-dasharray="4 3"/>
        <text x="255" y="72" font-size="9" fill="#0d9488">冷凝管</text>
        <text x="325" y="90" font-size="8" fill="#2563eb">↓冷水</text>
        <text x="325" y="120" font-size="8" fill="#dc2626">↑热水</text>
        <!-- 锥形瓶接收 -->
        <path d="M 330 140 L 320 175 L 350 175 Z" fill="#fef3c7" stroke="#d97706" stroke-width="2"/>
        <text x="315" y="190" font-size="9" fill="#475569">接收瓶</text>
        <!-- 酒精灯 -->
        <path d="M 130 200 L 170 200 L 160 180 L 140 180 Z" fill="#f59e0b" stroke="#d97706" stroke-width="1.5"/>
        <ellipse cx="150" cy="172" rx="12" ry="6" fill="#ef4444"/>
      </svg>
      <p class="apparatus-label">蒸馏装置</p>
    </div>`,

  extraction: `
    <div class="apparatus-item">
      <svg viewBox="0 0 280 240" xmlns="http://www.w3.org/2000/svg">
        <!-- 铁架台 -->
        <line x1="40" y1="20" x2="40" y2="220" stroke="#475569" stroke-width="3"/>
        <line x1="15" y1="220" x2="65" y2="220" stroke="#475569" stroke-width="4"/>
        <!-- 铁圈 -->
        <line x1="40" y1="80" x2="180" y2="80" stroke="#475569" stroke-width="2"/>
        <!-- 分液漏斗 -->
        <path d="M 140 40 L 140 60 L 110 130 L 110 170 L 130 170 L 130 185 L 130 185 L 130 170 L 150 170 L 150 185 L 150 170 L 170 170 L 170 130 L 140 60" fill="#fef3c7" stroke="#d97706" stroke-width="2"/>
        <!-- 上层（有机相） -->
        <path d="M 112 100 L 168 100 L 168 130 L 112 130 Z" fill="#fde68a" stroke="#d97706" stroke-width="1"/>
        <text x="175" y="115" font-size="9" fill="#92400e">有机相(上层)</text>
        <!-- 下层（水相） -->
        <path d="M 112 130 L 168 130 L 168 168 L 112 168 Z" fill="#bae6fd" stroke="#0284c7" stroke-width="1"/>
        <text x="175" y="150" font-size="9" fill="#0369a1">水相(下层)</text>
        <!-- 活塞 -->
        <rect x="120" y="170" width="40" height="8" fill="#475569"/>
        <!-- 漏斗颈 -->
        <line x1="140" y1="178" x2="140" y2="195" stroke="#d97706" stroke-width="2"/>
        <!-- 烧杯 -->
        <path d="M 105 195 L 105 225 L 175 225 L 175 195" fill="none" stroke="#94a3b8" stroke-width="2"/>
        <text x="115" y="218" font-size="9" fill="#475569">烧杯</text>
        <text x="80" y="50" font-size="9" fill="#475569">分液漏斗</text>
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

/* 引导：分级启发提问（伴思模式，逐级解锁） */
function guideHtml(g) {
  return `
    <p class="panel-note guide-note">
      伴思模式：只引导、不给答案——以下提问不包含任何结论，请按级思考，自己推出答案。
    </p>
    <div class="guide-toolbar">
      <button class="guide-expand-btn" onclick="toggleAllGuideLevels(this)">全部展开</button>
    </div>
    <div class="levels">
      ${g.levels.map((lv, i) => {
        const open = i === 0 ? 'open' : '';
        const lock = i === 0 ? '' : '<span class="lock-icon">🔒</span>';
        return `
        <details class="level" ${open}>
          <summary class="level-head">
            <span class="level-no">${lv.level}</span>
            <h5>${esc(lv.title)}</h5>
            ${lock}
          </summary>
          <ul>${lv.prompts.map((p) => `<li>${chem(p)}</li>`).join('')}</ul>
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

/* ---------- 错误提示 ---------- */

function showError(msg) {
  document.getElementById('loadingTip').hidden = true;
  const el = document.getElementById('errorTip');
  el.hidden = false;
  el.textContent = msg;
}
