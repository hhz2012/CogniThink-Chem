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

    <section class="panel" id="panel-solve">${solveHtml(s, p.category)}</section>
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
function solveHtml(s, category) {
  return `
    <p class="panel-note">引擎状态：${esc(s.message)}</p>
    ${answerHtml(s, category)}
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

function answerHtml(s, category) {
  if (!s.isSolved) return `<p class="answer-none">${esc(s.message)}</p>`;

  switch (category) {
    case 'ProductInference':
      return eqBlock('产物方程式', s.equations);

    case 'IonCoexistence': {
      const v = COEXIST_TEXT[s.coexistence] || s.coexistence || '';
      const cls = s.coexistence === 'Coexist' ? 'v-ok' : 'v-no';
      return `<div class="answer-verdict ${cls}">${esc(v)}</div>` +
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
      return `<div class="answer-block">${groups}</div>` + eqBlock('证据方程式', s.equations);
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
        </div>` + eqBlock('各步方程式', s.equations);
    }

    case 'StoichiometryCalc':
      return `
        <div class="answer-numeric">
          <b>${esc(s.numericValue)}</b><span>${esc(s.numericUnit)}</span>
          ${s.numericSpecies ? `<i>（${chem(s.numericSpecies)}）</i>` : ''}
        </div>`;

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
        </div>` + eqBlock('总反应', s.equations);
    }

    case 'ChemicalEquilibrium': {
      const q = s.equilibrium || {};
      const rows = (q.table || []).map((r) => `
        <tr>
          <td>${chem(r.species)}</td>
          <td>${esc(r.start)}</td>
          <td>${esc(r.change)}</td>
          <td>${esc(r.end)}</td>
        </tr>`).join('');
      const concl = [];
      if (q.conversion) concl.push(`${chem(q.conversionSpecies)} 转化率 <b>${esc(q.conversion)}</b>`);
      if (q.fraction) concl.push(`${chem(q.fractionSpecies)} 摩尔分数 <b>${esc(q.fraction)}</b>`);
      if (q.kValue) concl.push(`Kp = <b>${esc(q.kValue)}</b>`);
      if (q.direction) concl.push(`扰动后平衡 <b>${esc(DIRECTION_TEXT[q.direction] || q.direction)}</b>`);
      return `
        <div class="answer-block">
          <div class="eq-table-wrap">
            <table class="eq-table">
              <thead><tr><th>三段式（mol）</th><th>起始</th><th>转化</th><th>平衡</th></tr></thead>
              <tbody>${rows}</tbody>
            </table>
          </div>
          ${concl.length ? `<div class="eq-concl">${concl.map((c) => `<span>${c}</span>`).join('')}</div>` : ''}
        </div>` + eqBlock('可逆反应', s.equations);
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
      return chain + cons + eqBlock('弱过程方程', s.equations);
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
        </div>` + eqBlock('转化链方程式', s.equations);
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
        eqBlock('证据方程式', s.equations);
    }

    case 'OrganicInference': {
      const keys = Object.keys(s.substanceMap || {}).sort((a, b) => a.localeCompare(b, 'en'));
      const chain = keys.length
        ? `<div class="org-chain">${keys.map((k) =>
            `<span class="org-node"><b>${esc(k)}</b><i>${chem(s.substanceMap[k])}</i></span>`
          ).join('<span class="org-arrow">→</span>')}</div>`
        : '';
      return `<div class="answer-block">${chain}</div>` + eqBlock('各步方程式', s.equations);
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
        eqBlock('原理方程式', s.equations);
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
