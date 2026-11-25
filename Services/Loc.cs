using System.Globalization;

namespace DocGen.Services;

/// <summary>
/// All screen texts in Arabic and English. The language comes from the request culture
/// (cookie), so it needs no state: every call looks at the current culture.
/// </summary>
public sealed class Loc
{
    public static bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public static string Lang => IsArabic ? "ar" : "en";
    public static string Dir => IsArabic ? "rtl" : "ltr";

    public static string Text(string key) =>
        Map.TryGetValue(key, out var p) ? (IsArabic ? p.Ar : p.En) : key;

    public string this[string key] => Text(key);

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Text(key), args);

    /// <summary>Translates the fixed English messages that come from the services.</summary>
    public string Msg(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        return ServiceMessages.TryGetValue(message, out var key) ? Text(key) : message;
    }

    private static readonly Dictionary<string, string> ServiceMessages = new()
    {
        ["This username is already taken."] = "err.taken",
        ["LibreOffice was not found. Check the path in appsettings.json."] = "err.lo.notfound",
        ["LibreOffice finished but did not produce a PDF."] = "err.lo.nopdf",
        ["Could not start LibreOffice."] = "err.lo.start",
        ["The PDF conversion took too long."] = "err.lo.timeout",
        ["The PDF conversion failed."] = "err.lo.failed",
    };

    private static readonly Dictionary<string, (string Ar, string En)> Map = new()
    {
        // ---- Identity ----
        ["gov.country"] = ("جمهورية مصر العربية", "Arab Republic of Egypt"),
        ["gov.ministry"] = ("وزارة التربية والتعليم والتعليم الفني", "Ministry of Education and Technical Education"),
        ["app.name"] = ("منصة إصدار المستندات", "Document Generation Platform"),
        ["app.tagline"] = ("أنشئ مستنداتك الرسمية من القوالب المعتمدة بسهولة وسرعة.", "Create your official documents from approved templates quickly and easily."),
        ["footer.rights"] = ("© {0} وزارة التربية والتعليم والتعليم الفني. جميع الحقوق محفوظة.", "© {0} Ministry of Education and Technical Education. All rights reserved."),

        // ---- Navigation / top bar ----
        ["nav.menu"] = ("القائمة الرئيسية", "Main navigation"),
        ["nav.home"] = ("الرئيسية", "Home"),
        ["nav.templates"] = ("القوالب", "Templates"),
        ["nav.documents"] = ("مستنداتي", "My documents"),
        ["top.logout"] = ("تسجيل الخروج", "Log out"),
        ["lang.other"] = ("English", "العربية"),
        ["lang.title"] = ("Switch to English", "التبديل إلى اللغة العربية"),
        ["ui.error"] = ("حدث خطأ غير متوقع.", "An unhandled error has occurred."),
        ["ui.reload"] = ("إعادة تحميل", "Reload"),

        // ---- Auth ----
        ["auth.username"] = ("اسم المستخدم", "Username"),
        ["auth.password"] = ("كلمة المرور", "Password"),
        ["auth.confirm"] = ("تأكيد كلمة المرور", "Confirm password"),
        ["auth.point1"] = ("قوالب رسمية معتمدة", "Approved official templates"),
        ["auth.point2"] = ("معاينة فورية للمستند أثناء الكتابة", "Instant live preview while you type"),
        ["auth.point3"] = ("تنزيل بصيغة Word وPDF ومشاركة آمنة", "Download as Word and PDF, with secure sharing"),
        ["login.title"] = ("تسجيل الدخول", "Log in"),
        ["login.sub"] = ("أدخل بياناتك للوصول إلى حسابك.", "Enter your details to access your account."),
        ["login.noaccount"] = ("ليس لديك حساب؟", "No account?"),
        ["login.register"] = ("إنشاء حساب", "Register"),
        ["login.wrong"] = ("اسم المستخدم أو كلمة المرور غير صحيحة.", "Wrong username or password."),
        ["register.title"] = ("إنشاء حساب جديد", "Create account"),
        ["register.sub"] = ("أنشئ حسابك للبدء في إصدار المستندات.", "Create your account to start generating documents."),
        ["register.btn"] = ("إنشاء الحساب", "Register"),
        ["register.have"] = ("لديك حساب بالفعل؟", "Already have an account?"),
        ["val.required"] = ("هذا الحقل مطلوب.", "This field is required."),
        ["val.length"] = ("يجب أن يكون الطول بين {0} و{1} حرفاً.", "The length must be between {0} and {1} characters."),
        ["val.match"] = ("كلمتا المرور غير متطابقتين.", "Passwords do not match."),
        ["err.taken"] = ("اسم المستخدم مستخدم بالفعل.", "This username is already taken."),
        ["err.lo.notfound"] = ("لم يتم العثور على LibreOffice. تحقق من المسار في ملف appsettings.json.", "LibreOffice was not found. Check the path in appsettings.json."),
        ["err.lo.nopdf"] = ("انتهى LibreOffice دون إنتاج ملف PDF.", "LibreOffice finished but did not produce a PDF."),
        ["err.lo.start"] = ("تعذّر تشغيل LibreOffice.", "Could not start LibreOffice."),
        ["err.lo.timeout"] = ("استغرق تحويل PDF وقتاً طويلاً.", "The PDF conversion took too long."),
        ["err.lo.failed"] = ("فشل تحويل PDF.", "The PDF conversion failed."),

        // ---- Home ----
        ["home.hello"] = ("مرحباً، {0}", "Hello, {0}"),
        ["home.sub"] = ("أهلاً بك في منصة إصدار المستندات. اختر من أين تريد أن تبدأ.", "Welcome to the document generation platform. Choose where to start."),
        ["home.tpl.text"] = ("تصفّح القوالب المعتمدة وابدأ في إنشاء مستند جديد.", "Browse the approved templates and start a new document."),
        ["home.docs.text"] = ("اطلع على مستنداتك المحفوظة، ونزّلها أو شاركها.", "View your saved documents, download them or share them."),
        ["home.open"] = ("فتح", "Open"),
        ["home.steps"] = ("كيف تعمل المنصة؟", "How it works"),
        ["home.s1"] = ("اختر قالباً", "Choose a template"),
        ["home.s1.t"] = ("اختر القالب المناسب من قائمة القوالب.", "Pick the right template from the list."),
        ["home.s2"] = ("املأ البيانات", "Fill in the data"),
        ["home.s2.t"] = ("أدخل البيانات وشاهد المعاينة تتحدث فوراً.", "Enter the data and watch the preview update instantly."),
        ["home.s3"] = ("نزّل أو شارك", "Download or share"),
        ["home.s3.t"] = ("احفظ المستند ونزّله بصيغة Word أو PDF، أو شارك رابطاً آمناً.", "Save the document, download it as Word or PDF, or share a secure link."),

        // ---- Templates ----
        ["tpl.sub"] = ("اختر قالباً لإنشاء مستند جديد.", "Choose a template to create a new document."),
        ["tpl.empty"] = ("لا توجد قوالب بعد. انسخ ملف Word (.docx) إلى مجلد Templates ثم حدّث الصفحة.", "No templates yet. Copy a .docx file into the Templates folder and refresh this page."),
        ["tpl.nofields"] = ("لا توجد حقول. أضف عناصر نائبة مثل {{Name}} إلى المستند.", "No fields found. Add placeholders like {{Name}} to the document."),
        ["tpl.fields"] = ("عدد الحقول: {0}", "{0} field(s):"),
        ["tpl.use"] = ("استخدام القالب", "Use template"),

        // ---- Editor ----
        ["editor.title"] = ("المحرر", "Editor"),
        ["editor.back"] = ("القوالب", "Templates"),
        ["editor.backfull"] = ("العودة إلى القوالب", "Back to templates"),
        ["editor.notfound"] = ("القالب غير موجود.", "Template not found."),
        ["editor.unreadable"] = ("تعذّرت قراءة هذا القالب.", "This template could not be read."),
        ["editor.nofields"] = ("لا توجد حقول في هذا القالب.", "This template has no fields."),
        ["editor.data"] = ("بيانات المستند", "Document data"),
        ["editor.filename"] = ("اسم الملف", "File name"),
        ["editor.word"] = ("تنزيل Word", "Download Word"),
        ["editor.pdf"] = ("تنزيل PDF", "Download PDF"),
        ["editor.save"] = ("حفظ في مستنداتي", "Save to My documents"),
        ["editor.working"] = ("جارٍ التنفيذ...", "Working..."),
        ["editor.preview"] = ("المعاينة", "Preview"),
        ["editor.updating"] = ("جارٍ التحديث...", "Updating..."),
        ["editor.updatingaria"] = ("جارٍ تحديث المعاينة", "Updating preview"),
        ["editor.refresh"] = ("تحديث", "Refresh"),
        ["editor.saved"] = ("تم حفظ المستند.", "Document saved."),
        ["editor.open"] = ("فتح مستنداتي", "Open My documents"),
        ["editor.previewfail"] = ("تعذّر إنشاء المعاينة.", "The preview could not be created."),
        ["editor.wrong"] = ("حدث خطأ ما. يرجى المحاولة مرة أخرى.", "Something went wrong. Please try again."),
        ["editor.docname"] = ("اسم المستند", "Document name"),
        ["editor.autosave"] = ("سيتم حفظه في مستنداتي تلقائياً.", "It will be saved in My documents automatically."),
        ["editor.savedas"] = ("تم الحفظ في مستنداتي باسم \"{0}\"", "Saved in My documents as \"{0}\""),
        ["editor.saveerr"] = ("تعذّر الحفظ التلقائي.", "Could not save automatically."),
        ["editor.renameerr"] = ("تعذّرت إعادة تسمية المستند.", "Could not rename the document."),
        ["editor.dup1"] = ("تنبيه: يوجد مستند باسم \"{0}\" تم حفظه بتاريخ {1} (\"{2}\"). يمكنك المتابعة بشكل طبيعي.", "Heads up: a document for \"{0}\" was already saved on {1} (\"{2}\"). You can continue as normal."),
        ["editor.dupn"] = ("تنبيه: يوجد {0} مستندات باسم \"{1}\" محفوظة بالفعل (آخرها بتاريخ {2}). يمكنك المتابعة بشكل طبيعي.", "Heads up: {0} documents for \"{1}\" are already saved (latest: {2}). You can continue as normal."),
        ["home.count"] = ("المستندات التي تم إنشاؤها", "Documents generated"),
        ["home.viewdocs"] = ("عرض مستنداتي", "View my documents"),
        ["home.create"] = ("إنشاء مستند", "Create a document"),
        ["editor.previewtitle"] = ("معاينة المستند", "Document preview"),

        // ---- My documents ----
        ["docs.search"] = ("ابحث بالاسم...", "Search by name..."),
        ["docs.loading"] = ("جارٍ التحميل...", "Loading..."),
        ["docs.empty"] = ("لا توجد لديك مستندات بعد. افتح قالباً وسيتم حفظ المستند هنا تلقائياً.", "You have no documents yet. Open a template and it will be saved here automatically."),
        ["docs.view"] = ("عرض", "View"),
        ["docs.close"] = ("إغلاق", "Close"),
        ["docs.nomatch"] = ("لا توجد مستندات مطابقة للبحث.", "No documents match your search."),
        ["docs.name"] = ("الاسم", "Name"),
        ["docs.template"] = ("القالب", "Template"),
        ["docs.created"] = ("تاريخ الإنشاء", "Created"),
        ["docs.actions"] = ("الإجراءات", "Actions"),
        ["docs.share"] = ("مشاركة", "Share"),
        ["docs.delete"] = ("حذف", "Delete"),
        ["docs.confirmdelete"] = ("تأكيد الحذف", "Confirm delete"),
        ["docs.cancel"] = ("إلغاء", "Cancel"),
        ["share.title"] = ("مشاركة \"{0}\"", "Share \"{0}\""),
        ["share.note"] = ("يمكن لأي شخص لديه الرابط عرض ملف PDF هذا (للقراءة فقط) دون تسجيل الدخول، ولا يمكنه الاطلاع على بقية مستنداتك.", "Anyone with the link can view this PDF (read only) without logging in. They cannot see your other documents."),
        ["share.expires"] = ("ينتهي الرابط", "Link expires"),
        ["share.d1"] = ("بعد يوم واحد", "in 1 day"),
        ["share.d7"] = ("بعد 7 أيام", "in 7 days"),
        ["share.d30"] = ("بعد 30 يوماً", "in 30 days"),
        ["share.never"] = ("أبداً", "never"),
        ["share.create"] = ("إنشاء رابط", "Create link"),
        ["share.copynow"] = ("انسخ هذا الرابط الآن. لأسباب أمنية لا يمكن عرضه مرة أخرى (يمكنك دائماً إنشاء رابط جديد).", "Copy this link now. For security it cannot be shown again (you can always create a new one)."),
        ["share.copy"] = ("نسخ", "Copy"),
        ["share.whatsapp"] = ("إرسال عبر واتساب", "Send via WhatsApp"),
        ["share.copied"] = ("تم النسخ.", "Copied."),
        ["share.copyfail"] = ("تعذّر النسخ تلقائياً. حدّد الرابط واضغط Ctrl+C.", "Could not copy automatically. Select the link and press Ctrl+C."),
        ["share.nolinks"] = ("لا توجد روابط بعد.", "No links yet."),
        ["share.th.created"] = ("تاريخ الإنشاء", "Created"),
        ["share.th.expires"] = ("تاريخ الانتهاء", "Expires"),
        ["share.th.status"] = ("الحالة", "Status"),
        ["share.revoke"] = ("إلغاء الرابط", "Revoke"),
        ["share.fail"] = ("تعذّر إنشاء الرابط.", "The link could not be created."),
        ["status.Active"] = ("نشط", "Active"),
        ["status.Expired"] = ("منتهي", "Expired"),
        ["status.Revoked"] = ("ملغى", "Revoked"),

        // ---- Shared view ----
        ["shared.title"] = ("مستند مشارَك", "Shared document"),
        ["shared.invalid"] = ("هذا الرابط غير صالح. ربما انتهت صلاحيته أو تم إلغاؤه.", "This link is not valid. It may have expired or been revoked."),
        ["shared.download"] = ("تنزيل PDF", "Download PDF"),

        // ---- Not found / error ----
        ["notfound.title"] = ("الصفحة غير موجودة", "Not Found"),
        ["notfound.text"] = ("عذراً، المحتوى الذي تبحث عنه غير موجود.", "Sorry, the content you are looking for does not exist."),
        ["notfound.home"] = ("العودة إلى الرئيسية", "Back to home"),
        ["error.title"] = ("حدث خطأ.", "Error."),
        ["error.text"] = ("حدث خطأ أثناء معالجة طلبك.", "An error occurred while processing your request."),
        ["error.reqid"] = ("رقم الطلب", "Request ID"),

        // ---- Reconnect dialog ----
        ["rc.first"] = ("جارٍ إعادة الاتصال بالخادم...", "Rejoining the server..."),
        ["rc.repeat.a"] = ("فشلت إعادة الاتصال... ستتم المحاولة مجدداً خلال", "Rejoin failed... trying again in"),
        ["rc.repeat.b"] = ("ثانية.", "seconds."),
        ["rc.failed"] = ("تعذّرت إعادة الاتصال.", "Failed to rejoin."),
        ["rc.failed2"] = ("يرجى إعادة المحاولة أو إعادة تحميل الصفحة.", "Please retry or reload the page."),
        ["rc.retry"] = ("إعادة المحاولة", "Retry"),
        ["rc.paused"] = ("أوقف الخادم الجلسة مؤقتاً.", "The session has been paused by the server."),
        ["rc.resumefail"] = ("تعذّرت استعادة الجلسة.", "Failed to resume the session."),
        ["rc.resume"] = ("استئناف", "Resume"),
    };
}
