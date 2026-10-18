import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const packageDir = path.resolve(process.argv[2] || "artifacts/sunway-kluang-mall-data");
const outputPath = path.join(packageDir, "sunway-kluang-mall-index.xlsx");
const previewDir = path.join(packageDir, "previews");
const stores = JSON.parse(await fs.readFile(path.join(packageDir, "stores/stores.json"), "utf8"));
const events = JSON.parse(await fs.readFile(path.join(packageDir, "events/events.json"), "utf8"));
const deals = JSON.parse(await fs.readFile(path.join(packageDir, "deals/deals.json"), "utf8"));
const manifest = JSON.parse(await fs.readFile(path.join(packageDir, "manifest.json"), "utf8"));

const workbook = Workbook.create();
const summary = workbook.worksheets.add("Summary");
const storeSheet = workbook.worksheets.add("Stores");
const eventSheet = workbook.worksheets.add("Events");
const dealSheet = workbook.worksheets.add("Deals");
const fontFamily = "Arial";
const headerFill = "#7A1631";
const accentFill = "#F7E9EE";

function text(value) {
  return value == null ? "" : String(value).replace(/\s+/g, " ").trim();
}

function listCategories(item) {
  return (item.categoryTree || []).map(x => x.title).filter(Boolean).join("; ") || item.category?.title || "";
}

function isoDate(value) {
  if (!value) return null;
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? null : parsed;
}

function styleTable(sheet, rangeAddress, headerAddress, descriptionColumns = []) {
  const all = sheet.getRange(rangeAddress);
  all.format.font = { name: fontFamily, size: 10, color: "#222222" };
  all.format.verticalAlignment = "center";
  const header = sheet.getRange(headerAddress);
  header.format = {
    fill: headerFill,
    font: { name: fontFamily, size: 10, bold: true, color: "#FFFFFF" },
    horizontalAlignment: "center",
    verticalAlignment: "center",
    wrapText: true,
    borders: { preset: "inside", style: "thin", color: "#FFFFFF" },
  };
  header.format.rowHeight = 32;
  for (const column of descriptionColumns) {
    sheet.getRange(column).format.wrapText = true;
    sheet.getRange(column).format.verticalAlignment = "top";
  }
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
}

summary.showGridLines = false;
summary.getRange("A2:E2").merge();
summary.getRange("A2").values = [["Sunway Kluang Mall public data capture"]];
summary.getRange("A2").format.font = { name: fontFamily, size: 16, bold: true, color: "#222222" };
summary.getRange("A3:E3").format.borders = { bottom: { style: "medium", color: headerFill } };
summary.getRange("A5:B10").values = [
  ["Metric", "Value"],
  ["Stores captured", manifest.counts.stores_captured],
  ["Events captured", manifest.counts.events_captured],
  ["Deals captured", manifest.counts.deals_captured],
  ["Images/files downloaded", manifest.counts.files_downloaded],
  ["Download failures", manifest.counts.download_failures],
];
summary.getRange("A5:B5").format = { fill: headerFill, font: { name: fontFamily, bold: true, color: "#FFFFFF" }, horizontalAlignment: "center" };
summary.getRange("A6:A10").format.fill = accentFill;
summary.getRange("A6:A10").format.font = { name: fontFamily, bold: true };
summary.getRange("B6:B10").format.numberFormat = "#,##0";
summary.getRange("A12:B15").values = [
  ["Capture detail", "Value"],
  ["Source", manifest.source],
  ["Captured at (UTC)", `UTC ${manifest.captured_at_utc}`],
  ["Scope", manifest.scope],
];
summary.getRange("A12:B12").format = { fill: headerFill, font: { name: fontFamily, bold: true, color: "#FFFFFF" }, horizontalAlignment: "center" };
summary.getRange("A13:A15").format.fill = accentFill;
summary.getRange("A13:A15").format.font = { name: fontFamily, bold: true };
summary.getRange("B13:B15").format.wrapText = true;
summary.getRange("B14").format.numberFormat = "@";
summary.getRange("A2:B15").format.font = { name: fontFamily, size: 10 };
summary.getRange("A2").format.font = { name: fontFamily, size: 16, bold: true, color: "#222222" };
summary.getRange("A1").format.rowHeight = 12;
summary.getRange("A:A").format.columnWidth = 28;
summary.getRange("B:B").format.columnWidth = 75;

const storeHeaders = ["Title", "Category", "Level", "Venue", "Description", "Website", "Email", "Phone", "Facebook", "Instagram", "TikTok", "Xiaohongshu", "Source URL", "Main image URL", "Logo URL", "Slug", "ID"];
const storeRows = stores.map(item => {
  const contact = item.contact || {};
  const meta = item.mainImage?._meta;
  const logoMeta = item.logo?._meta;
  return [
    item.title || "", listCategories(item), item.level || "", item.venue || "", text(item.text), item.website || "",
    contact.email || "", contact.phone || "", item.facebook || "", item.instagram || "", item.tiktok || "", item.xiaohongshu || "",
    `https://www.sunwaykluangmall.com/directory/${item.slug || ""}`,
    meta ? `${meta.thumbsUrl}w768${meta.ext}` : "", logoMeta ? `${logoMeta.thumbsUrl}w768-crop${logoMeta.ext}` : "",
    item.slug || "", item.id || item._id || "",
  ];
});
storeSheet.getRange("A1").write([storeHeaders, ...storeRows]);
styleTable(storeSheet, `A1:Q${storeRows.length + 1}`, "A1:Q1", [`E2:E${storeRows.length + 1}`]);
storeSheet.getRange("A:Q").format.autofitColumns();
storeSheet.getRange("A:A").format.columnWidth = 24;
storeSheet.getRange("B:B").format.columnWidth = 24;
storeSheet.getRange("E:E").format.columnWidth = 48;
for (const c of ["F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q"]) storeSheet.getRange(`${c}:${c}`).format.columnWidth = 24;
storeSheet.getRange(`A2:A${storeRows.length + 1}`).format.wrapText = true;
storeSheet.getRange(`M2:O${storeRows.length + 1}`).format.wrapText = true;
storeSheet.tables.add(`A1:Q${storeRows.length + 1}`, true, "StoresTable");

const eventHeaders = ["Title", "Date display", "Description", "Detail text", "Terms and conditions", "Source URL", "Primary image URL", "Image count", "Slug"];
const eventRows = events.map(item => [item.title || "", item.date_display || "", item.description || "", item.detail_text || "", item.terms_and_conditions || "", item.source_url || "", (item.image_urls || [])[0] || "", (item.image_urls || []).length, item.slug || ""]);
eventSheet.getRange("A1").write([eventHeaders, ...eventRows]);
styleTable(eventSheet, `A1:I${eventRows.length + 1}`, "A1:I1", [`C2:E${eventRows.length + 1}`]);
eventSheet.getRange("A:I").format.autofitColumns();
eventSheet.getRange("A:A").format.columnWidth = 28;
eventSheet.getRange("B:B").format.columnWidth = 18;
eventSheet.getRange("C:C").format.columnWidth = 42;
eventSheet.getRange("D:E").format.columnWidth = 65;
eventSheet.getRange("F:I").format.columnWidth = 28;
eventSheet.getRange(`A2:B${eventRows.length + 1}`).format.wrapText = true;
eventSheet.getRange(`F2:I${eventRows.length + 1}`).format.wrapText = true;
eventSheet.getRange(`H2:H${eventRows.length + 1}`).format.numberFormat = "#,##0";
eventSheet.tables.add(`A1:I${eventRows.length + 1}`, true, "EventsTable");

const dealHeaders = ["Title", "Start date", "End date", "Shop", "Shop venue", "Description", "Terms", "Source URL", "Main image URL", "Slug", "ID"];
const dealRows = deals.map(item => {
  const shop = item.shopMeta || {};
  const meta = item.mainImage?._meta;
  return [item.title || "", isoDate(item.dateStart), isoDate(item.dateEnd), shop.title || "", shop.venue || "", text(item.description || item.text), text(item.term), `https://www.sunwaykluangmall.com/promotions/${item.slug || ""}`, meta ? `${meta.thumbsUrl}w768${meta.ext}` : "", item.slug || "", item.id || item._id || ""];
});
dealSheet.getRange("A1").write([dealHeaders, ...dealRows]);
styleTable(dealSheet, `A1:K${dealRows.length + 1}`, "A1:K1", [`F2:G${dealRows.length + 1}`]);
dealSheet.getRange(`B2:C${dealRows.length + 1}`).format.numberFormat = "yyyy-mm-dd";
dealSheet.getRange("A:K").format.autofitColumns();
dealSheet.getRange("A:A").format.columnWidth = 30;
dealSheet.getRange("B:C").format.columnWidth = 15;
dealSheet.getRange("D:E").format.columnWidth = 22;
dealSheet.getRange("F:G").format.columnWidth = 55;
dealSheet.getRange("H:K").format.columnWidth = 28;
dealSheet.getRange(`A2:A${dealRows.length + 1}`).format.wrapText = true;
dealSheet.getRange(`H2:K${dealRows.length + 1}`).format.wrapText = true;
dealSheet.tables.add(`A1:K${dealRows.length + 1}`, true, "DealsTable");

summary.tabColor = headerFill;
storeSheet.tabColor = "#A44762";
eventSheet.tabColor = "#BD7288";
dealSheet.tabColor = "#D59DAE";

workbook.recalculate();
await fs.mkdir(previewDir, { recursive: true });
for (const [name, range] of [["Summary", "A1:E16"], ["Stores", "A1:Q18"], ["Events", `A1:I${eventRows.length + 1}`], ["Deals", `A1:K${dealRows.length + 1}`]]) {
  const preview = await workbook.render({ sheetName: name, range, scale: 1, format: "png" });
  await fs.writeFile(path.join(previewDir, `${name.toLowerCase()}.png`), new Uint8Array(await preview.arrayBuffer()));
}

const inspect = await workbook.inspect({ kind: "table", range: "Summary!A1:B15", include: "values,formulas", tableMaxRows: 20, tableMaxCols: 4 });
await fs.writeFile(path.join(previewDir, "summary-inspect.ndjson"), inspect.ndjson, "utf8");
const errors = await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!", options: { useRegex: true, maxResults: 300 }, summary: "final formula error scan" });
await fs.writeFile(path.join(previewDir, "formula-errors.ndjson"), errors.ndjson, "utf8");

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, counts: { stores: stores.length, events: events.length, deals: deals.length } }, null, 2));
