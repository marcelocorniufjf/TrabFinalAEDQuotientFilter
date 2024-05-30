using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Sockets;

namespace QuotientFilterWinForms
{
    public class Slot
    {
        public int Quotient { get; set; }
        public int Remainder { get; set; }
        public bool BucketOccupied { get; set; }
        public bool RunContinued { get; set; }
        public bool IsShifted { get; set; }
        public int Key { get; set; }

        public Slot()
        {
            Quotient = 0;
            Remainder = 0;
            BucketOccupied = false;
            RunContinued = false;
            IsShifted = false;
            Key = -1; // Indica que não há chave associada
        }

        public override string ToString()
        {
            char bucket = BucketOccupied ? 'O' : 'E';
            char run = RunContinued ? 'R' : ' ';
            char shifted = IsShifted ? 'S' : ' ';
            return $"{bucket}{run}{shifted}";
        }
    }

    public static class HashFunctions
    {
        public static int Hash(int key, int q, int r)
        {
            // Utilize a versão MurmurHash3 32-bit para espalhamento eficiente
            int seed = 0; // Um valor constante pode ser utilizado como semente
            byte[] data = BitConverter.GetBytes(key);
            return MurmurHash3(data, seed) & ((1 << (q + r)) - 1); // Ajusta o valor hash para o tamanho necessário
        }

        private static int MurmurHash3(byte[] data, int seed)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;
            const uint r1 = 15;
            const uint r2 = 13;
            const uint m = 5;
            const uint n = 0xe6546b64;

            uint hash = (uint)seed;
            uint length = (uint)data.Length;
            int index = 0;

            while (length >= 4)
            {
                uint k = BitConverter.ToUInt32(data, index);
                k *= c1;
                k = RotateLeft(k, (int)r1);
                k *= c2;

                hash ^= k;
                hash = RotateLeft(hash, (int)r2);
                hash = hash * m + n;

                index += 4;
                length -= 4;
            }

            uint remainingData = 0;
            switch (length)
            {
                case 3:
                    remainingData ^= (uint)data[index + 2] << 16;
                    goto case 2;
                case 2:
                    remainingData ^= (uint)data[index + 1] << 8;
                    goto case 1;
                case 1:
                    remainingData ^= data[index];
                    remainingData *= c1;
                    remainingData = RotateLeft(remainingData, (int)r1);
                    remainingData *= c2;
                    hash ^= remainingData;
                    break;
            }

            hash ^= (uint)data.Length;
            hash = FMix(hash);

            return (int)hash;
        }

        private static uint RotateLeft(uint x, int r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private static uint FMix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return h;
        }
    }


    public class QuotientFilter
    {
        private int q;
        private int r;
        private int size;
        private Slot[] filter;
        private readonly PictureBox pictureBox;
        private string operationDetails;

        public QuotientFilter(int q, int r, PictureBox pictureBox)
        {
            this.q = q;
            this.r = r;
            this.size = 1 << q; // 2^q
            this.filter = new Slot[size];
            this.pictureBox = pictureBox;
            this.operationDetails = "";

            for (int i = 0; i < size; i++)
            {
                filter[i] = new Slot();
            }

            DrawFilter();
        }

        public void Insert(int key)
        {
            if (IsFull())
            {
                Resize();
            }

            int fingerprint = HashFunctions.Hash(key, q, r);
            int quotient = fingerprint >> r; // Primeiros q bits
            int remainder = fingerprint & ((1 << r) - 1); // Últimos r bits

            InsertSlot(quotient, remainder, key, quotient);
            operationDetails = $"Inserindo {key} (Q: {quotient}, R: {remainder})";
            DrawFilter();
        }

        public bool Lookup(int key)
        {
            int fingerprint = HashFunctions.Hash(key, q, r);
            int quotient = fingerprint >> r;
            int remainder = fingerprint & ((1 << r) - 1);

            int index = quotient;
            bool isFalsePositive = false;
            int count = 0;

            while (filter[index].BucketOccupied && count <= size)
            {
                if (filter[index].Remainder == remainder)
                {
                    if (filter[index].Key == key)
                    {
                        operationDetails = $"Encontrado {key} (Q: {quotient}, R: {remainder})";
                        HighlightSlot(index, Color.Green, key, quotient, remainder);
                        return true;
                    }
                    else
                    {
                        //isFalsePositive = true;
                        //break;
                    }
                }

                //if (!filter[index].RunContinued)
                //    break;

                index = (index + 1) % size;
                count++;
            }

            if (isFalsePositive)
            {
                operationDetails = $"Falso positivo {key} (Q: {quotient}, R: {remainder})";
                HighlightSlot(index, Color.Red, key, quotient, remainder);
            }
            else
            {
                operationDetails = $"Não encontrado {key} (Q: {quotient}, R: {remainder})";
                HighlightSlot(index, Color.Red, key, quotient, remainder);
            }
            return false;
        }

        private void DrawFilter(int highlightedIndex = -1, Color? highlightColor = null, int key = -1, int quotient = -1, int remainder = -1)
        {
            Bitmap bitmap = new Bitmap(pictureBox.Width, pictureBox.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);

                // Desenhar os índices dos slots na parte superior
                for (int i = 0; i < size; i++)
                {
                    DrawSlotIndex(g, i);
                }

                for (int i = 0; i < size; i++)
                {
                    Color color = filter[i].BucketOccupied ? Color.Blue : Color.White;
                    if (i == highlightedIndex && highlightColor.HasValue)
                    {
                        color = highlightColor.Value;
                    }
                    DrawSlot(g, i, color);
                    DrawMetadata(g, i, filter[i].ToString());
                    DrawBits(g, i, filter[i].Remainder, filter[i].Key);
                }

                // Desenhar os parâmetros do filtro na parte inferior
                DrawFilterParameters(g);

                // Desenhar os detalhes da operação na parte inferior
                DrawOperationDetails(g);

                // Se um slot foi destacado, exibir valores de quociente e remainder
                if (highlightedIndex != -1 && highlightColor.HasValue)
                {
                    int slotWidth = pictureBox.Width / size;
                    int x = highlightedIndex * slotWidth;
                    int y = 28 + ((pictureBox.Height - 80) / 2);
                    string qrValues = $"Q: {quotient}, R: {remainder}";
                    g.DrawString(qrValues, SystemFonts.DefaultFont, Brushes.Black, x, y + 40);
                }
            }
            pictureBox.Image = bitmap;
        }

        private void UpdateMetadataAfterDeletion(int startQuotient)
        {
            int index = startQuotient;

            while (filter[index].BucketOccupied)
            {
                filter[index].RunContinued = false;
                filter[index].IsShifted = false;

                // Verificar se o próximo slot pertence ao mesmo cluster
                int nextIndex = (index + 1) % size;
                if (filter[nextIndex].BucketOccupied && filter[nextIndex].IsShifted)
                {
                    filter[index].RunContinued = true;
                    filter[nextIndex].IsShifted = true;
                }
                index = nextIndex;
            }
        }

        public void Delete(int key)
        {
            int fingerprint = HashFunctions.Hash(key, q, r);
            int quotient = fingerprint >> r;
            int remainder = fingerprint & ((1 << r) - 1);

            int index = quotient;
            bool found = false;

            while (filter[index].BucketOccupied)
            {
                if (filter[index].Remainder == remainder && filter[index].Key == key)
                {
                    filter[index].BucketOccupied = false;
                    filter[index].Remainder = 0;
                    filter[index].Key = -1;
                    filter[index].RunContinued = false;
                    filter[index].IsShifted = false;
                    found = true;
                    break;
                }
                index = (index + 1) % size;
            }

            if (found)
            {
                // Atualizar os metadados dos slots subsequentes
                UpdateMetadataAfterDeletion(quotient);
            }

            operationDetails = found ? $"Deletando {key} (Q: {quotient}, R: {remainder})" : $"Não encontrado para deletar {key} (Q: {quotient}, R: {remainder})";
            DrawFilter();
        }

        //public bool Insert(int key)
        //{
        //    int quotient = key >> r; // Quociente
        //    int remainder = key & ((1 << r) - 1); // Resto

        //    for (int i = 0; i < size; i++)
        //    {
        //        int index = (quotient + i) % size;

        //        if (!filter[index].BucketOccupied)
        //        {
        //            InsertSlot(index, remainder, key, quotient);
        //            operationDetails = $"Inserindo {key} (Q: {quotient}, R: {remainder})";
        //            return true;
        //        }
        //        else if (filter[index].Quotient == quotient && filter[index].Remainder == remainder)
        //        {
        //            // Valor já presente
        //            return false;
        //        }
        //        else
        //        {
        //            // Deslocar o elemento atual e continuar a inserção
        //            int currentQuotient = filter[index].Quotient;
        //            int currentRemainder = filter[index].Remainder;
        //            bool currentIsShifted = filter[index].IsShifted;
        //            bool currentRunContinued = filter[index].RunContinued;

        //            InsertSlot(index, remainder, key, quotient);
        //            operationDetails = $"Inserindo {key} (Q: {quotient}, R: {remainder})";
        //            quotient = currentQuotient;
        //            remainder = currentRemainder;
        //            i = 0; // Reiniciar a busca para o próximo slot

        //            if (!currentIsShifted)
        //            {
        //                filter[index].RunContinued = true;
        //            }
        //        }
        //    }

        //    // Redimensionar e rehash se necessário
        //    Resize();

        //    return Insert(key);
        //}

        //private void InsertSlot(int index, int remainder, int key, int originalIndex)
        //{
        //    if (!filter[index].BucketOccupied)
        //    {
        //        filter[index].Quotient = originalIndex;
        //        filter[index].Remainder = remainder;
        //        filter[index].BucketOccupied = true;

        //        // Atualizar metadados
        //        if (index != originalIndex)
        //        {
        //            filter[index].IsShifted = true;
        //            filter[(index - 1 + size) % size].RunContinued = true;
        //        }

        //        DrawFilter();
        //        DrawMetadata(Graphics.FromHwnd(pictureBox.Handle), index, filter[index].ToString());
        //    }
        //    else
        //    {
        //        // Deslocar o elemento atual
        //        int currentQuotient = filter[index].Quotient;
        //        int currentRemainder = filter[index].Remainder;

        //        filter[index].Quotient = originalIndex;
        //        filter[index].Remainder = remainder;
        //        filter[index].IsShifted = true;
        //        filter[index].RunContinued = true;

        //        // Recursão para inserir o elemento deslocado no próximo slot
        //        InsertSlot((index + 1) % size, currentRemainder, key, currentQuotient);
        //    }
        //}

        private void Resize()
        {
            int newQ = q + 1; // Dobrar o tamanho do filtro
            int newSize = 1 << newQ; // Novo tamanho 2^(q+1)
            var newFilter = new Slot[newSize];
            for (int i = 0; i < newSize; i++)
            {
                newFilter[i] = new Slot();
            }

            // Guardar o filtro antigo
            var oldFilter = filter;
            var oldQ = q;
            var oldR = r;
            var oldSize = size;

            // Atualizar para o novo filtro
            q = newQ;
            size = newSize;
            filter = newFilter;

            // Reinserir os valores do filtro antigo no novo filtro
            foreach (var slot in oldFilter)
            {
                if (slot.BucketOccupied)
                {
                    int fingerprint = HashFunctions.Hash(slot.Key, q, r);
                    int quotient = fingerprint >> r; // Primeiros q bits
                    int remainder = fingerprint & ((1 << r) - 1); // Últimos r bits

                    InsertSlot(quotient, remainder, slot.Key, quotient);
                }
            }

            operationDetails = $"Filtro redimensionado para tamanho {size}";
        }

        //private void Resize()
        //{
        //    var oldBuckets = filter;
        //    size *= 2;
        //    filter = new Slot[size];

        //    for (int i = 0; i < size; i++)
        //    {
        //        filter[i] = new Slot();
        //    }

        //    foreach (var bucket in oldBuckets)
        //    {
        //        if (bucket.BucketOccupied)
        //        {
        //            Insert((bucket.Quotient << r) | bucket.Remainder);
        //        }
        //    }
        //}


        private void InsertSlot(int index, int remainder, int key, int originalIndex)
        {
            bool shifted = false;
            bool runContinued = false;

            while (filter[index].BucketOccupied)
            {
                if (filter[index].Key == key)
                {
                    operationDetails = $"Valor duplicado {key} não inserido.";
                    DrawFilter();
                    return;
                }

                // Marcar o slot original como continuado se estamos deslocando a partir do slot original
                if (index == originalIndex)
                {
                    runContinued = true;
                }

                int x = originalIndex;

                foreach (var slot in filter.Where(f => f.BucketOccupied == true && f.Quotient == originalIndex))
                {
                    slot.RunContinued = true;
                }

                index = (index + 1) % size;
                shifted = true;
            }

            filter[originalIndex].RunContinued = runContinued;

            filter[index].Quotient = originalIndex;
            filter[index].Remainder = remainder;
            filter[index].BucketOccupied = true;
            filter[index].IsShifted = shifted;
            filter[index].Key = key;
        }

        private bool IsFull()
        {
            foreach (var slot in filter)
            {
                if (!slot.BucketOccupied)
                {
                    return false;
                }
            }
            return true;
        }

        private void HighlightSlot(int index, Color color, int key, int quotient, int remainder)
        {
            DrawFilter(index, color, key, quotient, remainder);
        }

        private void DrawFilter()
        {
            Bitmap bitmap = new Bitmap(pictureBox.Width, pictureBox.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);

                // Desenhar os índices dos slots na parte superior
                for (int i = 0; i < size; i++)
                {
                    DrawSlotIndex(g, i);
                }

                for (int i = 0; i < size; i++)
                {
                    Color color = filter[i].BucketOccupied ? Color.Blue : Color.White;
                    DrawSlot(g, i, color);
                    DrawMetadata(g, i, filter[i].ToString());
                    DrawBits(g, i, filter[i].Remainder, filter[i].Key);
                }

                // Desenhar os parâmetros do filtro na parte inferior
                DrawFilterParameters(g);

                // Desenhar os detalhes da operação na parte inferior
                DrawOperationDetails(g);
            }
            pictureBox.Image = bitmap;
        }

        private void DrawSlot(Graphics g, int index, Color color)
        {
            int slotWidth = pictureBox.Width / size;
            int x = index * slotWidth;
            int y = 20; // Deixar espaço para os índices dos slots
            int slotHeight = (pictureBox.Height - 80) / 2; // Metade da altura disponível, menos espaço para índices e parâmetros
            g.FillRectangle(new SolidBrush(color), x, y, slotWidth, slotHeight);
            g.DrawRectangle(Pens.Black, x, y, slotWidth, slotHeight);
        }

        private void DrawMetadata(Graphics g, int index, string metadata)
        {
            int slotWidth = pictureBox.Width / size;
            int x = index * slotWidth;
            int y = (pictureBox.Height - 80) / 2 + 20;
            g.DrawString(metadata, SystemFonts.DefaultFont, Brushes.Black, x, y);
        }

        private void DrawSlotIndex(Graphics g, int index)
        {
            int slotWidth = pictureBox.Width / size;
            int x = index * slotWidth;
            g.DrawString(index.ToString(), SystemFonts.DefaultFont, Brushes.Black, x, 0);
        }

        private void DrawFilterParameters(Graphics g)
        {
            string parameters = $"Quotient bits: {q}, Remainder bits: {r}, Size: {size}";
            g.DrawString(parameters, SystemFonts.DefaultFont, Brushes.Black, 10, pictureBox.Height - 40);
        }

        private void DrawOperationDetails(Graphics g)
        {
            g.DrawString(operationDetails, SystemFonts.DefaultFont, Brushes.Black, 10, pictureBox.Height - 20);
        }

        private void DrawBits(Graphics g, int index, int remainder, int key)
        {
            int slotWidth = pictureBox.Width / size;
            int x = index * slotWidth;
            int y = (pictureBox.Height - 80) / 2 + 40;
            string bits = Convert.ToString(remainder, 2).PadLeft(r, '0');
            g.DrawString(bits, SystemFonts.DefaultFont, Brushes.Black, x, y);

            if (key != -1)
            {
                g.DrawString($"{key}", SystemFonts.DefaultFont, Brushes.Magenta, x, y + 15);
            }
        }        
    }
}
