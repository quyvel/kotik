using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Kotik
{
    /// One thing Ember noticed in the code.
    class Finding
    {
        public string File, Rule, Message;
        public int Line;
        public string Key { get { return System.IO.Path.GetFileName(File) + "|" + Rule + "|" + Fingerprint; } }
        public string Fingerprint = "";      // the trimmed line text: a dismissed finding stays dismissed while the line is the same
    }

    /// A small, offline code reviewer: patterns that compile fine but often do something other than intended.
    /// No AI, no network — only rules. It says "most likely" on purpose: you can answer "intended".
    static class Lint
    {
        public static List<Finding> Check(string path)
        {
            var list = new List<Finding>();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string[] raw;
            try { raw = File.ReadAllLines(path); } catch { return list; }
            if (raw.Length > 6000) return list;
            string[] code = Strip(raw, ext == ".py" ? "py" : "c");
            if (ext == ".py") Python(path, raw, code, list);
            else if (ext == ".cpp" || ext == ".cc" || ext == ".c" || ext == ".h" || ext == ".hpp" || ext == ".cxx") { Cpp(path, raw, code, list); CppStudy(path, raw, code, list); }
            return list;
        }

        /// Classic beginner C++ mistakes: arrays and '\0', sizeof of a pointer, a case without braces, a file opened without
        /// a check, new without delete, "<=" up to the array size written as a number.
        static void CppStudy(string file, string[] raw, string[] code, List<Finding> list)
        {
            string all = string.Join("\n", code);
            var sizes = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(all, @"\b[A-Za-z_][\w:<>]*\s+([A-Za-z_]\w*)\s*\[\s*(\d+)\s*\]"))
                sizes[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            bool afterCase = false;
            for (int i = 0; i < code.Length; i++)
            {
                string c = code[i], t = c.Trim(), r = raw[i];
                Match m;
                // for (...; i <= 5; ...) arr[i], where arr[5]
                m = Regex.Match(c, @"\bfor\s*\(.*;\s*(\w+)\s*<=\s*(\d+)\s*;");
                if (m.Success)
                {
                    int n = int.Parse(m.Groups[2].Value);
                    string idx = m.Groups[1].Value;
                    string body = c + (i + 1 < code.Length ? code[i + 1] : "") + (i + 2 < code.Length ? code[i + 2] : "");
                    foreach (var kv in sizes)
                        if (kv.Value == n && Regex.IsMatch(body, @"\b" + Regex.Escape(kv.Key) + @"\s*\[\s*" + Regex.Escape(idx) + @"\s*\]"))
                        {
                            Add(list, file, raw, i, "off-by-one", string.Format(L.T("«{0}» дойдёт до {1}, а у «{2}[{1}]» индексы только 0..{3}. Нужно «<».", "\"{0}\" reaches {1}, but \"{2}[{1}]\" only has indexes 0..{3}. You need \"<\"."), idx, n, kv.Key, n - 1));
                            break;
                        }
                }
                // char s[5] = "hello" — no room for '\0'
                m = Regex.Match(r, @"\bchar\s+(\w+)\s*\[\s*(\d+)\s*\]\s*=\s*""((?:[^""\\]|\\.)*)""");
                if (m.Success)
                {
                    int len = Regex.Replace(m.Groups[3].Value, @"\\.", "x").Length, n = int.Parse(m.Groups[2].Value);
                    if (len + 1 > n)
                        Add(list, file, raw, i, "no-terminator", string.Format(L.T("В «{0}[{1}]» строка из {2} символов не помещается вместе с '\\0' — нужно хотя бы [{3}].", "A {2}-character string doesn't fit into \"{0}[{1}]\" together with '\\0' — you need at least [{3}]."), m.Groups[1].Value, n, len, len + 1));
                }
                // sizeof of a pointer parameter: the size of the pointer (8), not of the array
                m = Regex.Match(c, @"\bsizeof\s*\(\s*(\w+)\s*\)");
                if (m.Success)
                {
                    string name = m.Groups[1].Value;
                    for (int k = i; k >= 0 && k > i - 40; k--)
                    {
                        var sig = Regex.Match(code[k], @"^\s*[\w:<>\*&\s]+\s+\w+\s*\(([^)]*)\)\s*\{?\s*$");
                        if (!sig.Success) continue;
                        if (Regex.IsMatch(sig.Groups[1].Value, @"\*\s*" + Regex.Escape(name) + @"\b|\b" + Regex.Escape(name) + @"\s*\[\s*\]"))
                            Add(list, file, raw, i, "sizeof-pointer", string.Format(L.T("«{0}» — параметр-указатель: sizeof даст размер указателя (8 байт), а не массива. Передай длину отдельно.", "\"{0}\" is a pointer parameter: sizeof gives the size of the pointer (8 bytes), not of the array. Pass the length separately."), name));
                        break;
                    }
                }
                // a declaration with initialisation right under a case without { }
                if (Regex.IsMatch(t, @"^(case\b.*[^:]|default\s*):\s*$")) afterCase = true;   // case Ticket::Adult: — "::" inside
                else if (t.StartsWith("{")) afterCase = false;
                else if (afterCase && Regex.IsMatch(t, @"^(const\s+)?[A-Za-z_][\w:<>]*\s+[A-Za-z_]\w*\s*(=|\{|\()") && !Regex.IsMatch(t, @"^(return|break|delete|case|std::cout|cout)\b"))
                {
                    bool laterCase = false;
                    for (int k = i + 1; k < code.Length && k < i + 30; k++)
                    {
                        string tk = code[k].Trim();
                        if (Regex.IsMatch(tk, @"^(case\b|default\s*:)")) { laterCase = true; break; }
                        if (tk == "}") break;
                    }
                    if (laterCase)
                        Add(list, file, raw, i, "case-scope", L.T("Переменная объявлена прямо в case без { } — компилятор ругнётся «jump to case label». Оберни тело case в фигурные скобки.", "A variable is declared right inside a case without { } — the compiler will complain \"jump to case label\". Wrap the case body in braces."));
                    afterCase = false;
                }
                // a file opened but not checked
                m = Regex.Match(c, @"\b(std::)?(i|o)?fstream\s+(\w+)\s*[\(\{;]");
                if (m.Success)
                {
                    string f = m.Groups[3].Value;
                    if (!Regex.IsMatch(all, @"\b" + Regex.Escape(f) + @"\.is_open\s*\(|!\s*" + Regex.Escape(f) + @"\b|\b(if|while)\s*\(\s*" + Regex.Escape(f) + @"\s*\)|\b" + Regex.Escape(f) + @"\.(fail|good)\s*\("))
                        Add(list, file, raw, i, "file-unchecked", string.Format(L.T("Файл «{0}» открыт, но нигде не проверено, открылся ли он (is_open() или if (!{0})). Если файла нет, программа молча прочитает пустоту.", "The file \"{0}\" is opened, but nothing checks whether it opened (is_open() or if (!{0})). If the file is missing, the program will silently read nothing."), f));
                }
                // new without delete
                m = Regex.Match(c, @"\b([A-Za-z_]\w*)\s*=\s*new\b");
                if (m.Success && !Regex.IsMatch(all, @"\bdelete\s*(\[\s*\])?\s*" + Regex.Escape(m.Groups[1].Value) + @"\b"))
                    Add(list, file, raw, i, "leak", string.Format(L.T("«{0}» создан через new, но нигде нет delete — утечка памяти.", "\"{0}\" is created with new, but there's no delete anywhere — a memory leak."), m.Groups[1].Value));
            }
        }

        static void Add(List<Finding> list, string file, string[] raw, int i, string rule, string msg)
        {
            list.Add(new Finding { File = file, Line = i + 1, Rule = rule, Message = msg, Fingerprint = raw[i].Trim() });
        }

        /// Blanks out comments and string/char literals so the rules only see code. Keeps line numbers.
        static string[] Strip(string[] lines, string lang)
        {
            var outp = new string[lines.Length];
            bool block = false;
            for (int n = 0; n < lines.Length; n++)
            {
                var s = lines[n];
                var sb = new StringBuilder(s.Length);
                char q = '\0';
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (block) { if (c == '*' && i + 1 < s.Length && s[i + 1] == '/') { block = false; i++; } sb.Append(' '); continue; }
                    if (q != '\0') { if (c == '\\') { sb.Append("  "); i++; continue; } if (c == q) q = '\0'; sb.Append(c == q ? c : ' '); continue; }
                    if (lang == "c" && c == '/' && i + 1 < s.Length && s[i + 1] == '/') break;
                    if (lang == "c" && c == '/' && i + 1 < s.Length && s[i + 1] == '*') { block = true; i++; sb.Append("  "); continue; }
                    if (lang == "py" && c == '#') break;
                    if (c == '"' || c == '\'') { q = c; sb.Append(c); continue; }
                    sb.Append(c);
                }
                outp[n] = sb.ToString();
            }
            return outp;
        }

        // ---------------- C / C++ ----------------

        static void Cpp(string file, string[] raw, string[] code, List<Finding> list)
        {
            var deleted = new Dictionary<string, int>();        // pointer name -> line of delete/free
            var arrays = new Dictionary<string, bool>();         // name -> allocated with new[]
            bool hasThread = false, hasJoin = false, hasMutex = false, hasDetach = false;
            int threadLine = -1;
            bool inSwitch = false;
            int caseLine = -1;
            bool caseHasBody = false, caseEnds = true;
            for (int i = 0; i < code.Length; i++)
            {
                string c = code[i];
                string t = c.Trim();
                if (t.Length == 0) continue;
                Match m;
                // use after delete / free
                foreach (var name in new List<string>(deleted.Keys))
                {
                    if (Regex.IsMatch(c, @"(?<![\*\.>]\s*)\b" + Regex.Escape(name) + @"\s*=[^=]")) { deleted.Remove(name); continue; }   // reassigned (not *p = …): fine
                    if (Regex.IsMatch(c, @"\b" + Regex.Escape(name) + @"\b") && !Regex.IsMatch(c, @"\b(delete|free)\b"))
                    {
                        Add(list, file, raw, i, "use-after-delete", string.Format(L.T("«{0}» используется после delete/free (строка {1}). Скорее всего висячий указатель.", "\"{0}\" is used after delete/free (line {1}). Most likely a dangling pointer."), name, deleted[name]));
                        deleted.Remove(name);
                    }
                }
                m = Regex.Match(c, @"\bdelete\s*(\[\s*\])?\s*([A-Za-z_]\w*)\s*;");
                if (m.Success)
                {
                    string name = m.Groups[2].Value;
                    bool arr = m.Groups[1].Success;
                    bool wasArr;
                    if (arrays.TryGetValue(name, out wasArr) && wasArr != arr)
                        Add(list, file, raw, i, "delete-mismatch", wasArr ? L.T("Массив создан через new[], а удаляется через delete без []. Нужно delete[].", "The array is created with new[] but deleted with delete without []. You need delete[].") : L.T("Удаляется через delete[], а создан через new без []. Нужно delete без [].", "It's deleted with delete[] but created with new without []. You need delete without []."));
                    bool nulled = i + 1 < code.Length && Regex.IsMatch(code[i + 1], @"\b" + Regex.Escape(name) + @"\s*=\s*(nullptr|NULL|0)\b");
                    if (!nulled) deleted[name] = i + 1;
                }
                m = Regex.Match(c, @"\bfree\s*\(\s*([A-Za-z_]\w*)\s*\)");
                if (m.Success) deleted[m.Groups[1].Value] = i + 1;
                m = Regex.Match(c, @"\b([A-Za-z_]\w*)\s*=\s*new\s+[\w:<>]+\s*(\[)?");
                if (m.Success) arrays[m.Groups[1].Value] = m.Groups[2].Success;
                // returning the address of a local
                m = Regex.Match(c, @"\breturn\s*&\s*([A-Za-z_]\w*)\s*;");
                if (m.Success && LocalDeclared(code, i, m.Groups[1].Value))
                    Add(list, file, raw, i, "return-local-addr", string.Format(L.T("Возвращается адрес локальной переменной «{0}». После выхода из функции её уже нет — получится висячий указатель.", "This returns the address of the local variable \"{0}\". It's gone once the function returns — a dangling pointer."), m.Groups[1].Value));
                // = instead of == in a condition
                if (Regex.IsMatch(c, @"\b(if|while)\s*\([^=!<>]*[^=!<>+\-*/%&|^]=[^=][^)]*\)") && !Regex.IsMatch(c, @"\b(if|while)\s*\(\s*\w[\w:<>]*\s+\w+\s*="))
                    Add(list, file, raw, i, "assign-in-cond", L.T("В условии одно «=» — это присваивание, а не сравнение. Скорее всего задумано «==».", "A single \"=\" in a condition is an assignment, not a comparison. Most likely you meant \"==\"."));
                // a semicolon right after if/for/while
                if (Regex.IsMatch(c, @"\b(if|while|for)\s*\(.*\)\s*;\s*$") && !Regex.IsMatch(t, @"^\}\s*while"))
                    Add(list, file, raw, i, "empty-body", L.T("После условия/цикла стоит «;». Тело выполнится не так, как кажется.", "There's a \";\" right after the condition/loop. The body won't run the way it looks."));
                // for (... i <= n ...) with arr[i] or .size()
                if (Regex.IsMatch(c, @"\bfor\s*\(.*;\s*\w+\s*<=\s*[\w.]+\s*(\.size\(\)|\.length\(\))?\s*;") && Regex.IsMatch(c, @"<=\s*[\w.]*(size|length|len|count|n|N)\b"))
                    Add(list, file, raw, i, "off-by-one", L.T("Цикл с «<=» до размера: последний шаг выйдет за границу массива. Скорее всего нужно «<».", "A loop with \"<=\" up to the size: the last step goes past the end of the array. Most likely you need \"<\"."));
                // integer division assigned to a floating variable
                if (Regex.IsMatch(c, @"\b(double|float)\s+\w+\s*=\s*\d+\s*/\s*\d+\s*;"))
                    Add(list, file, raw, i, "int-division", L.T("Деление двух целых чисел даёт целое, дробная часть пропадёт. Напиши, например, 1.0 / 3.", "Dividing two integers gives an integer; the fraction is lost. Write, for example, 1.0 / 3."));
                // unsafe C functions
                if (Regex.IsMatch(c, @"\b(gets|strcpy|strcat|sprintf)\s*\("))
                    Add(list, file, raw, i, "unsafe-func", L.T("Эта функция не проверяет размер буфера — легко переполнить память. Есть безопасные версии (fgets, strncpy, snprintf).", "This function doesn't check the buffer size — it's easy to overflow memory. There are safe versions (fgets, strncpy, snprintf)."));
                // comparing C strings with ==
                if (Regex.IsMatch(c, @"\bchar\s*\*") == false && Regex.IsMatch(c, @"==\s*""|""\s*==") && Regex.IsMatch(string.Join("\n", code), @"\bchar\s*\*"))
                    Add(list, file, raw, i, "cstr-compare", L.T("Сравнение со строкой через «==». Для char* это сравнение адресов, а не текста — нужен strcmp (или std::string).", "Comparing with a string using \"==\". For char* that compares addresses, not text — use strcmp (or std::string)."));
                // threads
                if (Regex.IsMatch(c, @"\bstd::thread\s+\w+|\bthread\s+\w+\s*\(|pthread_create\s*\(")) { hasThread = true; if (threadLine < 0) threadLine = i; }
                if (Regex.IsMatch(c, @"\.join\s*\(|pthread_join\s*\(")) hasJoin = true;
                if (Regex.IsMatch(c, @"\.detach\s*\(")) hasDetach = true;
                if (Regex.IsMatch(c, @"\b(std::)?mutex\b|lock_guard|unique_lock|scoped_lock|pthread_mutex|atomic<")) hasMutex = true;
                // fork without wait
                if (Regex.IsMatch(c, @"\bfork\s*\(") && !Regex.IsMatch(string.Join("\n", code), @"\bwait(pid)?\s*\("))
                    Add(list, file, raw, i, "fork-wait", L.T("fork() без wait()/waitpid(): дочерний процесс станет «зомби», когда завершится.", "fork() without wait()/waitpid(): the child process becomes a \"zombie\" when it finishes."));
                // recv/read result not checked
                if (Regex.IsMatch(t, @"^(recv|read|send|write)\s*\("))
                    Add(list, file, raw, i, "io-unchecked", L.T("Результат recv/read/send не проверяется. Они могут вернуть меньше байт, 0 (соединение закрыто) или -1 (ошибка).", "The result of recv/read/send isn't checked. They can return fewer bytes, 0 (connection closed) or -1 (error)."));
                // switch fall-through
                if (Regex.IsMatch(c, @"\bswitch\s*\(")) inSwitch = true;
                if (inSwitch && Regex.IsMatch(t, @"^(case\b.*|default\s*):"))
                {
                    if (caseLine >= 0 && caseHasBody && !caseEnds)
                        Add(list, file, raw, caseLine, "fallthrough", L.T("В этом case нет break — выполнение «провалится» в следующий case.", "This case has no break — execution \"falls through\" into the next case."));
                    caseLine = i; caseHasBody = false; caseEnds = false;
                    string after = Regex.Replace(t, @"^(case\b.*?[^:]|default\s*):(?!:)", "").Trim();   // enum class: "case X::Y:"
                    if (after.Length > 0) { caseHasBody = true; caseEnds = Regex.IsMatch(after, @"\b(break|return|continue|throw|goto)\b|\[\[fallthrough\]\]"); }
                }
                else if (inSwitch && caseLine >= 0 && t != "{" && t != "}")
                {
                    caseHasBody = true;
                    if (Regex.IsMatch(t, @"\b(break|return|continue|throw|goto)\b|\[\[fallthrough\]\]|exit\s*\(")) caseEnds = true;
                }
                if (inSwitch && t == "}" && caseLine >= 0) { inSwitch = false; caseLine = -1; }
            }
            if (hasThread && !hasJoin && !hasDetach)
                Add(list, file, raw, threadLine, "thread-join", L.T("Поток создаётся, но нигде нет join() или detach(). Программа упадёт, когда объект потока уничтожится.", "A thread is created, but there's no join() or detach(). The program will crash when the thread object is destroyed."));
            if (hasThread && !hasMutex && Regex.IsMatch(string.Join("\n", code), @"(\+\+|--|\+=|-=)"))
                Add(list, file, raw, threadLine, "thread-race", L.T("Потоки есть, а мьютекса или atomic нет. Если они меняют общие данные — будет гонка (результат «плавает»).", "There are threads but no mutex or atomic. If they change shared data, there will be a race (the result \"drifts\")."));
        }

        static bool LocalDeclared(string[] code, int line, string name)
        {
            for (int j = line; j >= 0 && j > line - 60; j--)
            {
                if (Regex.IsMatch(code[j], @"^\S.*\)\s*(const)?\s*\{?\s*$") && j != line) return false;   // reached the function header
                if (Regex.IsMatch(code[j], @"\b(int|double|float|char|bool|long|short|auto|std::\w+|[A-Z]\w*)\s+" + Regex.Escape(name) + @"\b\s*(=|;|\[|\{)") &&
                    !Regex.IsMatch(code[j], @"\bstatic\b")) return true;
            }
            return false;
        }

        // ---------------- Python ----------------

        static readonly string[] Builtins = { "list", "dict", "str", "int", "id", "input", "type", "len", "sum", "max", "min", "set", "file", "object" };

        static void Python(string file, string[] raw, string[] code, List<Finding> list)
        {
            bool inClass = false;
            int classIndent = 0;
            bool hasThread = false, hasLock = false, hasJoin = false;
            int threadLine = -1;
            for (int i = 0; i < code.Length; i++)
            {
                string c = code[i];
                string t = c.Trim();
                if (t.Length == 0) continue;
                int indent = c.Length - c.TrimStart().Length;
                Match m;
                if (Regex.IsMatch(t, @"^class\s+\w+")) { inClass = true; classIndent = indent; continue; }
                if (inClass && indent <= classIndent && !t.StartsWith("@")) inClass = false;
                // mutable default arguments
                if (Regex.IsMatch(t, @"^def\s+\w+\s*\(.*=\s*(\[\s*\]|\{\s*\}|set\(\s*\))"))
                    Add(list, file, raw, i, "mutable-default", L.T("Изменяемое значение по умолчанию ([] или {}) создаётся один раз и общее для всех вызовов. Лучше None, а внутри создавать новое.", "A mutable default value ([] or {}) is created once and shared by all calls. Use None and create a new one inside."));
                // a method without self
                m = Regex.Match(t, @"^def\s+(\w+)\s*\(([^)]*)\)");
                if (m.Success && inClass && indent > classIndent)
                {
                    string args = m.Groups[2].Value.Trim();
                    bool decorated = i > 0 && Regex.IsMatch(code[i - 1].Trim(), @"^@(staticmethod|classmethod)");
                    if (!decorated && !Regex.IsMatch(args, @"^(self|cls)\b"))
                        Add(list, file, raw, i, "no-self", string.Format(L.T("Метод «{0}» внутри класса без self. При вызове obj.{0}() будет ошибка.", "The method \"{0}\" inside a class has no self. Calling obj.{0}() will fail."), m.Groups[1].Value));
                }
                // is with literals
                if (Regex.IsMatch(c, @"\bis\s+(not\s+)?(\d+|""|'|\[|\()") || Regex.IsMatch(c, @"(\d+|""|')\s+is\s"))
                    Add(list, file, raw, i, "is-literal", L.T("«is» сравнивает объекты, а не значения. Для чисел и строк нужно «==».", "\"is\" compares objects, not values. For numbers and strings use \"==\"."));
                // == None
                if (Regex.IsMatch(c, @"(==|!=)\s*None\b"))
                    Add(list, file, raw, i, "eq-none", L.T("С None лучше сравнивать через «is None» / «is not None».", "Compare with None using \"is None\" / \"is not None\"."));
                // bare except
                if (Regex.IsMatch(t, @"^except\s*:"))
                    Add(list, file, raw, i, "bare-except", L.T("Голый «except:» ловит вообще всё, даже Ctrl+C. Лучше указать тип ошибки, например except ValueError:.", "A bare \"except:\" catches everything, even Ctrl+C. Better name the error type, for example except ValueError:."));
                // shadowing builtins
                m = Regex.Match(t, @"^([a-z_]+)\s*=[^=]");
                if (m.Success && Array.IndexOf(Builtins, m.Groups[1].Value) >= 0)
                    Add(list, file, raw, i, "shadow-builtin", string.Format(L.T("Имя «{0}» уже занято встроенной функцией. После этого {0}() перестанет работать.", "The name \"{0}\" belongs to a built-in function. After this, {0}() stops working."), m.Groups[1].Value));
                // modifying a list while iterating over it
                m = Regex.Match(t, @"^for\s+\w+\s+in\s+(\w+)\s*:");
                if (m.Success)
                {
                    string lst = m.Groups[1].Value;
                    for (int j = i + 1; j < code.Length && j < i + 25; j++)
                    {
                        string cj = code[j];
                        if (cj.Trim().Length == 0) continue;
                        if (cj.Length - cj.TrimStart().Length <= indent) break;
                        if (Regex.IsMatch(cj, @"\b" + Regex.Escape(lst) + @"\.(remove|append|pop|insert)\s*\("))
                        {
                            Add(list, file, raw, j, "mutate-while-iter", string.Format(L.T("Список «{0}» меняется прямо во время цикла по нему — элементы будут пропускаться. Иди по копии: for x in {0}[:].", "The list \"{0}\" changes while you loop over it — items will be skipped. Loop over a copy: for x in {0}[:]."), lst));
                            break;
                        }
                    }
                }
                // threads and locks
                if (Regex.IsMatch(c, @"threading\.Thread\s*\(|\bThread\s*\(")) { hasThread = true; if (threadLine < 0) threadLine = i; }
                if (Regex.IsMatch(c, @"\bLock\s*\(|\bRLock\s*\(|with\s+\w*lock")) hasLock = true;
                if (Regex.IsMatch(c, @"\.join\s*\(")) hasJoin = true;
                // sockets
                if (Regex.IsMatch(c, @"\.recv\s*\(\s*\d+\s*\)") && !Regex.IsMatch(string.Join("\n", code), @"while"))
                    Add(list, file, raw, i, "recv-once", L.T("recv() может вернуть только часть сообщения. Обычно читают в цикле, пока не придёт всё.", "recv() may return only part of the message. Usually you read in a loop until everything arrives."));
                if (Regex.IsMatch(c, @"=\s*socket\.socket\s*\(") && !Regex.IsMatch(string.Join("\n", code), @"\.close\s*\(|with\s+socket"))
                    Add(list, file, raw, i, "socket-close", L.T("Сокет создаётся, но нигде не закрывается. Используй with socket.socket(...) as s: или s.close().", "A socket is created but never closed. Use with socket.socket(...) as s: or s.close()."));
                // open without with/close
                if (Regex.IsMatch(c, @"=\s*open\s*\(") && !Regex.IsMatch(string.Join("\n", code), @"\.close\s*\("))
                    Add(list, file, raw, i, "file-close", L.T("Файл открыт без with и без close(). Надёжнее: with open(...) as f:.", "The file is opened without with and without close(). Safer: with open(...) as f:."));
            }
            if (hasThread && !hasJoin)
                Add(list, file, raw, threadLine, "py-thread-join", L.T("Поток запускается, но нигде нет join(). Программа может закончиться раньше потока (или не дождаться результата).", "A thread is started, but there's no join(). The program may finish before the thread (or not wait for its result)."));
            if (hasThread && !hasLock && Regex.IsMatch(string.Join("\n", code), @"(\+=|-=)"))
                Add(list, file, raw, threadLine, "py-thread-race", L.T("Потоки меняют данные без Lock. «+=» из нескольких потоков может терять обновления.", "Threads change data without a Lock. \"+=\" from several threads can lose updates."));
        }

        // ---------------- new topics: Ember gets curious ----------------

        public static string[][] Topics { get { return new[]
        {
            new[] { @"#include\s*<thread>|std::thread|pthread_create", L.T("потоки", "threads"), L.T("Потоки! Главное правило: общие данные — только под мьютексом. И не забывай join().", "Threads! The main rule: shared data only under a mutex. And don't forget join().") },
            new[] { @"std::mutex|lock_guard|pthread_mutex", L.T("мьютексы", "mutexes"), L.T("Мьютексы — отлично. lock_guard сам отпустит замок, даже если вылетит исключение.", "Mutexes, great. lock_guard releases the lock by itself, even if an exception flies.") },
            new[] { @"std::atomic", L.T("atomic", "atomic"), L.T("atomic — это когда без мьютекса, но только для простых операций. Красиво.", "atomic: no mutex, but only for simple operations. Neat.") },
            new[] { @"std::condition_variable", L.T("условные переменные", "condition variables"), L.T("condition_variable! Не забудь про ложные пробуждения: жди в цикле с условием.", "condition_variable! Remember spurious wakeups: wait in a loop with a condition.") },
            new[] { @"\bfork\s*\(", L.T("процессы", "processes"), L.T("fork()! Теперь у тебя два процесса. Родителю стоит дождаться ребёнка через wait().", "fork()! Now you have two processes. The parent should wait for the child with wait().") },
            new[] { @"#include\s*<sys/socket.h>|winsock2\.h|\bsocket\s*\(", L.T("сокеты", "sockets"), L.T("Сокеты — это сети! Помни: recv может вернуть меньше, чем ты ждёшь.", "Sockets mean networking! Remember: recv may return less than you expect.") },
            new[] { @"std::unique_ptr|std::shared_ptr|make_unique|make_shared", L.T("умные указатели", "smart pointers"), L.T("Умные указатели — и висячих указателей станет намного меньше. Я одобряю.", "Smart pointers, and far fewer dangling pointers. I approve.") },
            new[] { @"\btemplate\s*<", L.T("шаблоны", "templates"), L.T("Шаблоны! Сложно, но мощно. Ошибки компилятора будут длинными — это нормально.", "Templates! Hard but powerful. Compiler errors will be long, and that's normal.") },
            new[] { @"^\s*import\s+threading|from\s+threading", L.T("потоки в Python", "Python threads"), L.T("Потоки в Python: из-за GIL они хороши для ожидания (сеть, файлы), а не для вычислений.", "Python threads: because of the GIL they're good for waiting (network, files), not for computing.") },
            new[] { @"^\s*import\s+socket|from\s+socket", L.T("сокеты в Python", "Python sockets"), L.T("Сокеты в Python! with socket.socket() as s: — и закрывать не надо.", "Python sockets! with socket.socket() as s: and no need to close it.") },
            new[] { @"^\s*import\s+asyncio|\basync\s+def\b", L.T("asyncio", "asyncio"), L.T("async/await! Один поток, много задач. Главное — не вызывай долгие функции без await.", "async/await! One thread, many tasks. Just don't call slow functions without await.") },
            new[] { @"^\s*import\s+multiprocessing|from\s+multiprocessing", L.T("процессы в Python", "Python processes"), L.T("multiprocessing — настоящие параллельные вычисления в Python. Не забудь if __name__ == '__main__':", "multiprocessing is real parallel computing in Python. Don't forget if __name__ == '__main__':") },
            new[] { @"^\s*class\s+\w+.*:\s*$", L.T("классы в Python", "Python classes"), L.T("Классы в Python! Первый аргумент метода — self, это сам объект.", "Python classes! The first argument of a method is self, the object itself.") },
            new[] { @"^\s*@\w+", L.T("декораторы", "decorators"), L.T("Декораторы — функции, которые оборачивают функции. Уровень растёт!", "Decorators are functions that wrap functions. Level up!") },
            new[] { @"^\s*import\s+os|from\s+os\b|import\s+subprocess", L.T("работа с ОС", "working with the OS"), L.T("Работа с системой из Python. subprocess.run(..., check=True) — и ошибки не потеряются.", "Working with the system from Python. subprocess.run(..., check=True) and errors won't get lost.") },
        }; } }
    }
}
