using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LBWmodifier
{
    public class CSVUtils
    {
        public static string[][] LoadFromCSVFile(string file_name)
        {
            return LoadFromCSVString(File.ReadAllText(file_name));
        }

        public static string[][] LoadFromCSVString(string file_string)
        {
            int file_length = file_string.Length;

            // read char by char and when a , or \n, perform appropriate action
            int cur_file_index = 0; // index in the file
            var all_lines = new List<List<string>>();
            List<string> cur_line = new List<string>(); // current line of data
            int max_column = 0;
            StringBuilder cur_item = new StringBuilder("");
            bool inside_quotes = false; // managing quotes
            while (cur_file_index < file_length)
            {
                char c = file_string[cur_file_index++];

                switch (c)
                {
                    case '"':
                        if (!inside_quotes)
                        {
                            inside_quotes = true;
                        }
                        else
                        {
                            if (cur_file_index == file_length)
                            {
                                // end of file
                                inside_quotes = false;
                                goto case '\n';
                            }
                            else if (file_string[cur_file_index] == '"')
                            {
                                // double quote, save one
                                cur_item.Append("\"");
                                cur_file_index++;
                            }
                            else
                            {
                                // leaving quotes section
                                inside_quotes = false;
                            }
                        }
                        break;
                    case '\r':
                        // ignore it completely
                        break;
                    case ',':
                        goto case '\n';
                    case '\n':
                        if (inside_quotes)
                        {
                            // inside quotes, this characters must be included
                            cur_item.Append(c);
                        }
                        else
                        {
                            // end of current item
                            cur_line.Add(cur_item.ToString());
                            cur_item.Length = 0;
                            if (c == '\n' || cur_file_index == file_length)
                            {
                                // 读取到了行结尾
                                max_column = Math.Max(max_column, cur_line.Count);
                                all_lines.Add(cur_line);
                                cur_line = new List<string>();
                            }
                        }
                        break;
                    default:
                        // other cases, add char
                        cur_item.Append(c);
                        break;
                }
            }

            var result = new string[all_lines.Count][];
            for (int i = 0; i < all_lines.Count; i++)
            {
                result[i] = new string[max_column];
                for (int j = 0; j < all_lines[i].Count; j++)
                {
                    result[i][j] = all_lines[i][j];
                }
                for (int j = all_lines[i].Count; j < max_column; j++)
                {
                    // 多余的空行用空字符填充
                    result[i][j] = "";
                }
            }

            return result;
        }

        public static string[][] LoadFromFileNei(AFileBase file)
        {
            using (var csv = new CsvParser())
            {
                if (!csv.Open(file))    
                    return new string[0][];
                var result = new string[csv.max_cell_y][];
                for (int y = 0; y < csv.max_cell_y; y++)
                {
                    result[y] = new string[csv.max_cell_x];
                    for (int x = 0; x < csv.max_cell_x; x++)
                    {
                        result[y][x] = csv.GetCellAsString(x, y);
                    }
                }
                return result;
            }
        }

        public static string[][] LoadFromFileNei(string nei_path)
        {
            using (var system = new FileSystemWindows())
            {
                system.SetBaseDirectory(Path.GetDirectoryName(Path.GetFullPath(nei_path)));
                system.AddFolder("");
                return LoadFromFileNei(system.FileOpen(Path.GetFileName(nei_path)));
            }
        }

        public static void SaveArray2CSVFile(string file_name, string[][] array)
        {
            using (var output = new StreamWriter(File.OpenWrite(file_name), Encoding.UTF8))
            {
                output.Write(TurnArray2CSVString(array));
            }
        }

        public static string TurnArray2CSVString(string[][] array)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < array.Length; i++)
            {
                for (int j = 0; j < array[i].Length; j++)
                {
                    var text = array[i][j];
                    if (text.Contains("\"") || text.Contains("\n"))
                        text = "\"" + text.Replace("\"", "\"\"") + "\"";
                    sb.Append(text);
                    if (j != array[i].Length - 1)
                    {
                        sb.Append(",");
                    }
                }
                if (i != array.Length - 1)
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
