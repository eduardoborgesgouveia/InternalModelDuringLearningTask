using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Globalization;
using Biolab_DataAcquisition;

namespace SVIPT_BioLab
{
    public partial class Form_Main : Form
    {
        #region Defines

        //Constantes definem dimensões (% das dimensões w e h do painel que mantém os elementos)
        //e as posições horizontais de cada elemento relativas(%).
        private double vertPos = 0.45;    //posição vertical (centro de cada elemento em relação à altura total do painel)
        //dimensões em proporção às dimensões do painel
        private double cursorWidth = 0.03;  //Cursor (deve ser menor que target_H1234_Width)
        private double cursorHeight = 0.10;
        private double target_H1234_Height = 0.25;
        //Posicionamento das metas na região entre o lado direito de Home e
        //o lado direito da última meta (T2).
        //O posicionamento aqui indica o centro da meta.
        private double pos_T2 = 0.875; //87.5%
        //Espaço horizontal ocupado pelas metas 1,2,3 e 4
        private double target_H1234_Width = 0.12;

        //Limite de posicionamento à direita e à esquerda (em relação ao centro dos objetos)
        private int rightLim, leftLim;

        //Objeto para comunicação serial
        private TeensyUsbCom tSerialPortObj;
        //Este objeto conterá os diversos elementos associados ao equipamento,
        //firmware, status, dados de experimento etc.
        private TeensyData tDataObj;

        //Experimento em execução?
        private bool running;

        //Objeto para conter a sessão
        private Session theSession;

        //um contador para definir a quantidade de amostras novas a serem usadas para calcular
        //a nova posição do cursor durante uma tarefa SVIPT.
        private int countNewSamples;

        //Objeto que controla uma thread
        //Utilizada para a aquisição da célula de carga
        ThreadHandler threadAcquisitionHandler;
        ThreadHandler threadGUIHandler;
        private Queue<double> dataQueue;

        //Método que realiza a leitura dos dados enviados pelo Teensy
        //O sinal da célula de carga passa por um filtro de média móvel    
        byte[] oneNumbAsByte = new byte[2];//variavel temporaria auxiliar para leitura dos bytes
        bool fim = false; //fim = true se acabou os dados daquele pacote
        bool gotPackSize = false; //true se ja leu o cabecalho e o tamanho do pacote de dados             
        //declarando as variaves necessarias
        int i, indicePackHeader = 0;
        int PackSize = 0;
        double[] currentPackage = new double[10];//10 pq esse eh o tamanho maximo de um pacote mandado pelo teensy
        char[] packageHeader;
        //Contador de alvos atingidos
        private int targetCounter = 0;
        //Contador de trials (sequência de alvos)
        private int trialCounter = 0;

		//Contador de idas 
		private int contadorIda = 0;
		//contador para pausas
		private int contadorToStop = 0;
		//flag para os blocos que mudam curva
		private bool flagToChangeCurve = false;


        //Variável que define o estado do cursor
        private int cursorState = 0;
        //Definindo os possíveis estados do cursor
        private const int cursorAtHome = 0; //Cursor localizado em Home
        private const int cursorMovingTarget = 1; //Movendo em direção à algum alvo (esquerda pra direita)        
        private const int cursorHitTarget = 2; //Cursor atingiu o centro do alvo
        private const int cursorOvershoot = 3; //Cursor passou pelo alvo      
        private const int cursorUndershoot = 4; //Cursor não atingiu o alvo  
        //Guarda a posição inicial do cursor que indica que ele está em Home
        private int cursorHome = 0;
        //Limites dos alvos
        private int leftLimits;
        private int rightLimits;
        //Percentual do comprimento total onde estao localizados cada um dos 5 alvos
        private double[] tgtPercents;
        //Variável para verificar oscilação no movimento do cursor
        public double maxCursorPosition = 0;
        //Variável que armazena a posição do cursor no momento do undershoot
        private double cursorPosUndershoot = 0;

		//Variavel para caminho de save 

		string pathToSave = null;

		bool flagToInvisible = false;

		#endregion

		public Form_Main()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            //objeto para conter dados recebidos etc
            tDataObj = new TeensyData();

            //objecto para o canal de comunicação USB com Teensy 2.0
            tSerialPortObj = new TeensyUsbCom(ref tDataObj);

            //criar handler para capturar evento OnMessageReceived do objeto TeensyUsbCom tSerialPortObj.
            // tSerialPortObj.OnMessageReceived += serialReadPack_OnMessageReceived;

            //objeto para conter a sessão em andamento
            theSession = null; //por hora nenhuma.

            countNewSamples = 0;
        }

        private void Form_Main_Load(object sender, EventArgs e)
        {
            //Inicializa o objeto que controla a thread de aquisição
            this.threadAcquisitionHandler = new ThreadHandler(Aquisicao);
            //Inicializa o objeto que controla a thread de atualização da interface gráfica
            this.threadGUIHandler = new ThreadHandler(MoveCursorGUI);
            //Inicializa a fila que armazena os dados coletados da célula de carga
            this.dataQueue = new Queue<double>();

           

            this.setElementsPosition();
            //cursor on top
            pictureBox_Cursor.BringToFront();
            //o painel panel_GoStop será usado como botão de início
            label_GoStop.Text = "---";
            //Experimento em execução?
            running = false;

            //todos os controles desabilitados
            disableAllControls();
            MenuConectar.Enabled = true;
			
		}
		#region enable and disable controls (barra superior)
		private void disableAllControls()
        {
            menuTS_Sessao.Enabled = false;
            MenuConectar.Enabled = false;
            menuTS_SessaoNova.Enabled = false;
            menuTS_SessaoSalvar.Enabled = false;
            menuTS_Calibrar.Enabled = false;
            menuTS_MapearSessao.Enabled = false;
            menuTS_iniciarAquisi.Enabled = false;
            panel_GoStop.Enabled = false;
            label_GoStop.Enabled = false;
            pictureBox_GoStop.Enabled = false;
        }

        private void enableAllControls()
        {
            menuTS_Sessao.Enabled = true;
            MenuConectar.Enabled = true;
            menuTS_SessaoNova.Enabled = true;
            menuTS_SessaoSalvar.Enabled = true;
            menuTS_Calibrar.Enabled = true;
            menuTS_MapearSessao.Enabled = true;
            menuTS_iniciarAquisi.Enabled = true;
            panel_GoStop.Enabled = true;
            label_GoStop.Enabled = true;
            pictureBox_GoStop.Enabled = true;
        }
		#endregion

		#region posicao das coisas na tela, Handshake, e GO/STOP
		//Estabelecer handshake com Teensy
		private void handshake()
        {
            //Abrir canal de comunicação USB com MIP3
            label_Status.Text = "Canal de comunicação Host<->Teensy estabelecido via USB.";

            //Teensy ainda não está pronto.
            tDataObj.ready = false;

            //Tentar handshake
            //Se uma aquisição estiver em andamento (este software foi encerrado e iniciado sem resetar o Teensys)
            //devemos parar qualquer coletar em andamento e depoius esvaziar o buffer de leitura.
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StopAquis);
            //aguarde alguns milisegundos para o Teensy parar qualquer transmissão
            Thread.Sleep(1000);
            //esvaziar buffer de leitura da serial
            tSerialPortObj.flushSerialInputBuffer();
            tDataObj.serialDataBC.clear();

            //1- Ler firmware
            //    Host envia query.
            //byte[] CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_SendFmw);
            //tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_SendFmw);

            label_Status.Text = "Solicitação de versão de firmware enviada.\nAguarde...";

            // Teensy deve responder enviando mensagem com o firmware.
            //    Aguardar responder ou estourar timeOut... 1 segundo Timeout
            bool res = tDataObj.waitValue(TeensyComCommands.tCMD_SendFmw, 3000);
			//verificar o retorno de WaitValue ==> False == não LEU (timeOut)
			if (!res)
            {
                //Não enviou firmware
                tDataObj.firmwareVersion = "";
                tDataObj.handShakeOk = false;
                tDataObj.ready = false;
                label_Status.Text = "Não consigo ler firmware do Teensy.\nAbortar.";
                return;
            }
            //é o firmware correto?
            if (tDataObj.firmwareVersion != TeensyUsbComConstants.Firmware)
            {
                //Não enviou firmware
                tDataObj.firmwareVersion = "";
                tDataObj.handShakeOk = false;
                tDataObj.ready = false;
                label_Status.Text = "Firmware errado no Teensy.\nAbortar.";
                return;
            }

            //testar...

            label_Status.Text = "Firmware ok";

            //2 - ler dados do conversor AD do Teensy

            //2.1 - Quantidade de bits
            //    Host envia query.
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_SendADnbits);
            res = tDataObj.waitValue(TeensyComCommands.tCMD_SendADnbits, 2000);
            if (!res)
            {
                //Não enviou nBits
                tDataObj.ad_nBits = -1;
                tDataObj.ready = false;
                label_Status.Text = "Não consigo ler quantidade de bits do AD do Teensy.\nAbortar.";
                return;
            }
            //2.2 - Fundo de escala - range
            //    Host envia query.
            //CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_SendADrange);
            //tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_SendADrange);
            res = tDataObj.waitValue(TeensyComCommands.tCMD_SendADrange, 2000);
            if (!res)
            {
                //Não enviou raange
                tDataObj.ad_Range = -1;
                tDataObj.ready = false;
                label_Status.Text = "Não consigo ler range do AD do Teensy.\nAbortar.";
                return;
            }

            //2.3 - Taxa de amostragem usada para digitalizar o sinal
            //    Host envia query.
            //CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_SendADtxAmost);
            //tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_SendADtxAmost);
            res = tDataObj.waitValue(TeensyComCommands.tCMD_SendADtxAmost, 2000);
            if (!res)
            {
                //Não enviou raange
                tDataObj.ad_TxAmost = -1;
                tDataObj.ready = false;
                label_Status.Text = "Não consigo ler taxa de amostragem do sinal.\nAbortar.";
                return;
            }

            //Tudo certo
            tDataObj.ready = true;

            //5 - Handshake finalizado - Teensy aguarda comandos do Host.
            return;
        }

        //Definir a posição das metas e do curso dentro do Painel que os contém.
        private void setElementsPosition()
        {
            //cursor
            pictureBox_Cursor.Height = (int)(panel_fdbkArea.Height * cursorHeight);
            pictureBox_Cursor.Width = (int)(panel_fdbkArea.Width * cursorWidth);

            //Target Home - deve conter o cursor com folga de 2 pixels de cada lado
            pictureBox_Thome.Height = (int)(panel_fdbkArea.Height * target_H1234_Height);
            pictureBox_Thome.Width = pictureBox_Cursor.Width + 4;
            //T2_Left
            pictureBox_T2_Left.Height = pictureBox_Thome.Height;
            pictureBox_T2_Left.Width = pictureBox_Thome.Width;
            //T2_Right
            pictureBox_T2_Right.Height = pictureBox_Thome.Height;
            pictureBox_T2_Right.Width = pictureBox_Thome.Width;


            //Posição vertical (centro vertical do objeto em relação à altura do painel)
            pictureBox_Thome.Top = ((int)(panel_fdbkArea.Height * vertPos)) - (pictureBox_Thome.Height / 2);
            pictureBox_T2_Left.Top = pictureBox_Thome.Top;
            pictureBox_T2_Right.Top = pictureBox_Thome.Top;

            //Cursor e home na metada das demais targets
            pictureBox_Cursor.Top = (pictureBox_Thome.Top + (pictureBox_Thome.Height / 2)) - (pictureBox_Cursor.Height / 2);
            

            //Distribuição horizontal

            //limite de posicionamento à direita
            rightLim = panel_fdbkArea.Width - (panel_fdbkArea.Width * 5 / 100);
            //limite de posicionamento à esquerda
            leftLim = panel_fdbkArea.Width * 5 / 100;
            //Posicionar Thome à esquerda
            pictureBox_Thome.Left = leftLim;
            //Ajustar limite de posicionamento à esquerda para o centro de Home
            leftLim = leftLim + pictureBox_Thome.Width / 2;

            //Todos os elementos serão posicionado entre o centro de Home (0%) e rightLim (100%)
            int spaceLim = rightLim - leftLim;

            //Quantidade de pixels ocupados na horiz pelas metas
            int target_H1234_Width_px = (int)(target_H1234_Width * spaceLim);

            //Cursor dentro de Thome
            pictureBox_Cursor.Left = pictureBox_Thome.Left + 2;

           
            //T2 - centro em pos_T2
            pictureBox_T2_Left.Left = leftLim + (int)((spaceLim * pos_T2) -
                                                       (target_H1234_Width_px / 2));
            pictureBox_T2_Right.Left = pictureBox_T2_Left.Left + (int)(target_H1234_Width_px) -
                                       pictureBox_T2_Right.Width;

            
            //Go/Stop
            panel_GoStop.Top = pictureBox_Thome.Bottom + 80;
            panel_GoStop.Left = leftLim + (int)(spaceLim * 0.4);
            //   label_GoStop.Top = pictureBox_GoStop.Top + 4;
            //   label_GoStop.Left = pictureBox_GoStop.Right + 10;

            //Origem do cursor --> Home
            cursorHome = pictureBox_Cursor.Left;

            //Atualiza os objetos gráficos da interface da sessão
            updateSessionTargets();

            //Atualiza os limites de movimento do cursor
            updateCursorLimits();
        }

        //Atualiza os limites de movimentação do cursor
        private void updateCursorLimits()
        {
            if (theSession != null)
            {
                int[] lim = new int[2];
                lim[0] = leftLim;
                lim[1] = rightLim;
                theSession.setCursorLimits(lim);
            }
        }

        private void updateSessionTargets()
        {
            if (theSession != null)
            {
                PictureBox[,] _sessionTargets = new PictureBox[1, 2];
                _sessionTargets[0, 0] = pictureBox_T2_Left;
                _sessionTargets[0, 1] = pictureBox_T2_Right;
                
                theSession.setGUIobjects(pictureBox_Cursor, pictureBox_Thome, _sessionTargets);
            }
        }

        //Move o cursor na horizontal, para a posição PERCENTUAL, dentro dos limites definidos por
        //rightLim, leftLim; onde leftLim = 0% e rightLim = 100%
        private void setCursorPercPos(double posPercent)
        {
            int d = rightLim - leftLim;

            //posição desejada dentro dos limites horizontais - em pixels
            if (posPercent < 0) posPercent = 0;
            if (posPercent > 100) posPercent = 100;

            int pos = (int)(posPercent * d / 100);

            pictureBox_Cursor.Left = (pos - pictureBox_Cursor.Width / 2) + leftLim;
        }

        private void panel_GoStop_MouseClick(object sender, MouseEventArgs e)
        {
            handleClick_GoStop();
        }

        private void label_GoStop_MouseClick(object sender, MouseEventArgs e)
        {
            handleClick_GoStop();
        }

        private void pictureBox_GoStop_Click(object sender, EventArgs e)
        {
            handleClick_GoStop();
        }

        private void handleClick_GoStop()
        {
            //Teensy Ready?
            if (!tDataObj.ready)
            {
                label_GoStop.Text = "---";
                return;
            }

            //Experimento em execução?
            if (running)
            {
                //parar experimento
                stopSVIPT();
                running = false;
                label_GoStop.Text = "Click to begin";
            }
            else
            {
                //iniciar experimento
                startSVIPT();
                running = true;
                label_GoStop.Text = "Go";
            }
        }
		#endregion

		#region Start/Stop Data Comunication
		//iniciar experimento SVIPT
		private void startSVIPT()
        {
            countNewSamples = 0;
            //indicar sessão não salva
            theSession.saved = false;
       
            //Iniciar coleta de dados
            //    Host envia query.
            byte[] CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_StartAquis);
            tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);
        }

        //parar experimento SVIPT
        private void stopSVIPT()
        {
            //parar coleta de dados
            byte[] CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_StopAquis);
            tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);
        }
		#endregion

		private void Form_Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (theSession == null)
                return; //fechar direto

            //parar qualquer envio de dados que esteja ocorrendo no Teensy
            byte[] CmdBytes = ASCIIEncoding.ASCII.GetBytes(TeensyComCommands.tCMD_StopAquis);
            tSerialPortObj.TransmitBytes(CmdBytes, TeensyComCommands.tCMDSize);

            //existem dados não salvos desta sessão?
            if (!theSession.saved)
            {
                DialogResult res = MessageBox.Show("As alterações na sessão atual não foram salvas e serão perdidas.\nDeseja continuar?", "Atenção", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (res == DialogResult.Cancel)
                {
                    e.Cancel = true; //não fechar
                    return;
                }
            }

            //salvar configuração atual
            theSession.saveConfig();

            //carry on - close main form
        }

        private void panel_fdbkArea_Resize(object sender, EventArgs e)
        {
            setElementsPosition();
        }

        private void menuTS_SessaoNova_Click(object sender, EventArgs e)
        {
            DialogResult res;

            //Criar um objeto para conter a nova sessão.
            //Apenas uma sessão pode existir.
            if (theSession != null)
            {
                //a sessão atual será descartada.
                if (!theSession.saved && theSession.data.Count > 0)
                {
                    res = MessageBox.Show("Os dados da sessão atual não foram salvos e serão descartados. \nDeseja continuar?", "Atenção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (res == DialogResult.No)
                        return;
                }
                //caso contrário vamos continuar usando a configuração da sessão atual,
                //exceto seus dados.
                theSession.data.Clear(); ///MUDAR para a lista em teensyData

                //Atualiza os limites de movimento do cursor
                updateCursorLimits();
                //Atualiza os targets
                updateSessionTargets();
            }
            else
            {
                //criar um novo objeto para a sessão
                theSession = new Session();
                //Atualiza os limites de movimento do cursor
                updateCursorLimits();
                //Atualiza os targets
                updateSessionTargets();
            }

            //Taxa de amostragem usada na coleta de dados desta sessão
            theSession.Sampling_Rate = tDataObj.ad_TxAmost;

            //Abrir dialog para definir novo nome para a sessão e voluntário.
            NewSession newSessionForm = new NewSession(ref theSession);
            res = newSessionForm.ShowDialog();
            //ok?
            if (res == DialogResult.OK)
            {
                enableAllControls();
                //hardware ok?
                if (tDataObj.ready)
                {
                    //ok - pronto para iniciar sessão
                    label_GoStop.Text = "Go/Stop";
                }
                else
                {
                    //não - desabilitar controles de start
                    panel_GoStop.Enabled = false;
                    label_GoStop.Enabled = false;
                    pictureBox_GoStop.Enabled = false;
                    label_GoStop.Text = "---";
                    return;
                }
            }
            else
            {
                disableAllControls();
                label_GoStop.Text = "---";
                menuTS_SessaoNova.Enabled = true;
            }


        }

        private void menuTS_Calibrar_Click(object sender, EventArgs e)
        {
            if (theSession == null) return;

            //Abrir dialog para definir calibração.
            Form calibF = new Calibrar(ref theSession, ref tSerialPortObj, ref tDataObj);
            calibF.ShowDialog();

            // label_Status.Text = theSession.taskMaxForcePercentage.ToString();
        }


		#region Chama o mapeamento do experimento (olhar depois para colocar as variaçãoes das curvas de força)
		private void menuTS_Mapear_Click(object sender, EventArgs e)
        {
            if (theSession == null) return;

            Form mapTaskF = new MapSession(ref theSession);
            mapTaskF.ShowDialog();

            // label_Status.Text = theSession.taskMaxForcePercentage.ToString();
        }
		#endregion

		private void menuTS_SessaoSalvar_Click(object sender, EventArgs e)
        {
            //salvar sessão atual em arquivo
            theSession.saveDataFile(pathToSave);
            //indicar sessão salva
            theSession.saved = true;
        }

        

   

        //Define a posição horizontal do cursor (entre 0.0 e 1.0), conforme a curva de 
        //transformação "Força x Deslocamento" - transformação log (ln) => F = e^(k*X).
        //Ajustando para o formato da curva de resposta desejada e para o 
        //limite de força do experimento:
        //1 - Definindo o deslocamento horizontal (X) entre 0 e 1.0 (inclusive).
        //2 - Para atingir deslocamento max (1.0) e mapear a F*X em ln, definimos a equação:
        //      F = Fmax_task * e^(k*X - k) ------ e: exp()
        //3 - Assim, para encontrar o deslocamento (entre 0 e 1), dado um nível de força qualquer:
        //      X = 1 + ln(F/Fmax_task)/k
        //4 - para ajustar à curva dos artigos de SVIPT: k = 5

        //Return the normalized (0-1) cursor position from force level
        private double getNormalizedCursorPos(double f)
        {
			double maxForcePercent = theSession.maxForcePercentage;
			maxForcePercent = maxForcePercent / 100;
			double limiar = (theSession.maxForceVal - theSession.baseLineForceVal) * maxForcePercent * theSession.forcePercentageToChangeCurve;

			if (f <= 0)
                return 0;
			//tentar fazer a alteração da equação aqui 
			else if (theSession.SVIPT_Task == sviptConstant.sviptTaskLinear && !flagToChangeCurve)
			{
			
				TbDebug.Invoke(new Action(() => TbDebug.Text = "Linear"));
				double local = (f / ((theSession.maxForceVal - theSession.baseLineForceVal) * maxForcePercent));
				return local;
				
			}
			else
			{
				if (f > limiar)
				{
					double x1 = f;
					double xmin1 = limiar;//0
					double xmax1 = (theSession.maxForceVal - theSession.baseLineForceVal) * maxForcePercent;// * maxForcePercent;//0.8
					double ymin1 = 0;
					double ymax1 = 6;
					double convertion = ((x1 - xmin1) * (ymax1 - ymin1) / (xmax1 - xmin1)) + ymin1;


					double x = Math.Exp(convertion);
					double xmin = Math.Exp(0);//0??
					double xmax = Math.Exp(6);
					double ymin = (limiar / ((theSession.maxForceVal - theSession.baseLineForceVal) * maxForcePercent));
					double ymax = 1;
					double cursorPos = (((x - xmin) * (ymax - ymin) / (xmax - xmin)) + ymin);
					return cursorPos;

				}
				else
				{
					return (f / ((theSession.maxForceVal - theSession.baseLineForceVal) * maxForcePercent));
				}
			}
		}

        private void MenuConectar_Click(object sender, EventArgs e)
        {
            Conectar conectF = new Conectar(ref theSession, ref tSerialPortObj, ref tDataObj);
            conectF.formMain = this;
            conectF.ShowDialog();
        }

        //Plota o cursor na posição horizontal definida (dentro dos limites de deslocamento)
        private void setCursorHorizontalPos(int posHoriz)
        {
            //Posicionar centro horizontal do curso em posHoriz
            pictureBox_Cursor.Left = posHoriz - (int)(pictureBox_Cursor.Width / 2);
            Application.DoEvents();
            //pictureBox_Cursor.Location = new Point ((posHoriz - (int)(pictureBox_Cursor.Width / 2)) , pictureBox_Cursor.Location.Y );
        }

        private void menuTS_iniciarAquisi_Click(object sender, EventArgs e)
        {
            
        }

		#region iniciar/parar/Stop aquisição

		private void iniciarAquisiçãoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (theSession == null)
                return;
    
            if (theSession.SVIPT_Task == sviptConstant.sviptTaskModifed || theSession.SVIPT_Task == sviptConstant.sviptTaskModifed2)
            {
                Random rnd = new Random();//random
                int[] NCadaCurva = new int[7];//quantos A, A'1, A'2, A'3, A'4, A'5 e A'6 ainda faltam
                for (int n = 1; n< 7; n++) NCadaCurva[n] = theSession.SVIPT_TaskRepetitions/8;
                NCadaCurva[0] = theSession.SVIPT_TaskRepetitions/4;//8A e 4 de cada A'
                for (int TRep = 0; TRep < theSession.SVIPT_TaskRepetitions; TRep++)
                    {
                        theSession.ListaDeCurvas[TRep] = rnd.Next(8);    // 0 <= rnd < 8 ou seja , rnd =0,1,2,3,4,5,6 ou 7
                        if (theSession.ListaDeCurvas[TRep] == 7)//Pra que A tenha chance dobrada, se for 0 ou 8 eh A, os outro 1->A'1, 2->A'2, etc
                            theSession.ListaDeCurvas[TRep] = 0;
                        while (NCadaCurva[theSession.ListaDeCurvas[TRep]] == 0)//se ja tiver chegado em 0, roda denovo ate cair em uma das curvas que nao chegou em 0 ainda
                        {
                            theSession.ListaDeCurvas[TRep] = rnd.Next(8);
                            if (theSession.ListaDeCurvas[TRep] == 7)
                                theSession.ListaDeCurvas[TRep] = 0;
                        }
                        NCadaCurva[theSession.ListaDeCurvas[TRep]]--;
                    }
             }

                int i = 0;
            for (i = 0; i < theSession.SVIPT_TaskRepetitions; i++)
                for (int j = 0; j < theSession.numberOfTrials; j++)
                {//como eh percentual vai de -1 a 1 (-1 caso cursor em zero e alvo em 100%)
                    //assim, nunca sera exatamente -1 ou +1 (pois os alvos estao entre 20% e 87.5%)
                    theSession.error[i, j] = -1;
                    //inicializando tempo pra -1 pq um tempo coletado jamais sera negativo
                    theSession.times[i, j] = -1;
                }

            //zerando o valor máximo do cursor
            maxCursorPosition = 0;

            //zerando os indicadores de resultados
            theSession.clearMeasures();

			// inicializando o array dos target
			//inicializando os arrays com limites da direita e da esquerda de cada um dos targets
			//Limites à esquerda

			leftLimits = pictureBox_T2_Left.Left + pictureBox_T2_Left.Width / 2;
			//Limites à direita
			rightLimits = pictureBox_T2_Right.Right + pictureBox_T2_Left.Width / 2;




			//inicializando o array com as porcentagens de cada target
			tgtPercents = new double[theSession.numberOfTrials];
            tgtPercents[0] = 0.875;

            //Muda o label para "GO"
            label_GoStop.Text = "GO";

            //Colocando o cursor na condição parado
            this.cursorState = cursorAtHome;

            //Zerando os contadores da sessão
            this.targetCounter = 0;
            this.trialCounter = 0;

            //Inicialização das variáveis necessárias à comunicação com o Teensy
            packageHeader = new char[TeensyComCommands.tCMDSize];
            for (i = 0; i < TeensyComCommands.tCMDSize; i++)
                packageHeader[i] = '\0'; //inicializando o array
                                         //label_Status.Text = "Alvo atual: " + (TgtAtual + 1);
                                         //comeca a pegar sinal
                                         //aguardar chegada do header desejado no buffer serialDataBC.
                                         //NOTE QUE tudo que chegar antes será descartado do buffer circular.
                                         //Começa a aquisição

            //Limpa os dados da sessão previamente salvos
            theSession.data.Clear();
            
            //Envia o sinal para iniciar a aquisição
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StartAquis);

            //Inicializa a thread de aquisição
            if (this.threadAcquisitionHandler.isRunning() == false)
                this.threadAcquisitionHandler.Start();
            else
                this.threadAcquisitionHandler.Resume();

            //Inicializa a thread de processamento (atualiza o movimento do cursor)
            if (this.threadGUIHandler.isRunning() == false)
                this.threadGUIHandler.Start();
            else
                this.threadGUIHandler.Resume();
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
        }

        private void pararToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopAcquisition();
        }

        private void StopAcquisition()
        {
            //Encerrando aquisição            
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StopAquis);
            Thread.Sleep(100);
            tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StopAquis);
            //aguarde alguns milisegundos para o Teensy parar qualquer transmissão
            Thread.Sleep(100);
            //esvaziar buffer de leitura da serial
            tSerialPortObj.flushSerialInputBuffer();
            tDataObj.serialDataBC.clear();

            //Pausa a thread de aquisição
            threadAcquisitionHandler.Pause();
            //Pausa a thread que atualiza o cursor
            threadGUIHandler.Pause();
        }

		#endregion
		private void stopForAWhile()
		{
			this.threadAcquisitionHandler.Pause();
			this.threadGUIHandler.Pause();
			//Encerrando aquisição            
			tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StopAquis);
			Thread.Sleep(100);
			tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StopAquis);
			//aguarde alguns milisegundos para o Teensy parar qualquer transmissão
			Thread.Sleep(100);
			//esvaziar buffer de leitura da serial
			tSerialPortObj.flushSerialInputBuffer();
			tDataObj.serialDataBC.clear();
			dataQueue.Clear();

		}
		private void returnAfterWhile()
		{
			tSerialPortObj.flushSerialInputBuffer();
			tDataObj.serialDataBC.clear();
			dataQueue.Clear();

			//Envia o sinal para iniciar a aquisição
			tSerialPortObj.TransmitMsg(TeensyComCommands.tCMD_StartAquis);

			this.threadAcquisitionHandler.Resume();
			this.threadGUIHandler.Resume();
		}
		//Função de atualização da GUI
		//Movimenta o cursor de acordo com o sinal da célula de carga
		private void MoveCursorGUI()
        {
            //Mutex para sincronização das threads
            this.threadAcquisitionHandler.mutex.WaitOne();
            int numSamples = dataQueue.Count;            
            //Processamento
            //Mover o cursor a intervalos de 50mS.
            int interval = 50;
            //Quantidade de amostras em interval_mSeg = (TxAmos * interval) /1000
            int nEpoch40 = (int)(theSession.Sampling_Rate * interval / 1000);

			bool flagToHit = false;
			bool flagToHome = true;

            //Se existe a quantidade de amostras necessárias para atualizar a posição
            //do cursor
            if (numSamples >= nEpoch40)//#####################################################################################
            {

                theSession.EpochQueue.Enqueue(numSamples.ToString() + "\t" + nEpoch40.ToString());

                //Vetor auxiliar
                double[] samples = new double[numSamples];
                //Retira da fila os dados a serem processados e os
                //armazena em um vetor auxiliar
                for (int i = 0; i < nEpoch40; i++) //numSamples; i++)
                    samples[i] = dataQueue.Dequeue();
                //Libera o mutex
                this.threadAcquisitionHandler.mutex.ReleaseMutex();

                //calcular média das últimas nEpoch40 amostras.
                double sumAvg = 0;
                for (int i = 0; i < nEpoch40; i++)
                {
                    sumAvg = sumAvg + samples[i];
                }
                sumAvg = (sumAvg) / nEpoch40; //média
                sumAvg = (sumAvg * (tDataObj.ad_Range / Math.Pow(2, tDataObj.ad_nBits)) - theSession.baseLineForceVal);
				//esta chegando como 0 - 4096 bits, entao convertemos a media para 0-5v (preferi deixar a lista em 0-1023 e converter apenas a media de cada nEpoch pra manter o processo rapido);
				sumAvg = Math.Round(sumAvg, 8);
                //O quanto mover?
                double xn = getNormalizedCursorPos(sumAvg); //retorna posição normalizada (entre 0 e 1)

				//abel1.Invoke(new Action(() => label1.Text = sumAvg.ToString()));

				//O cursor se move entre os limites rightLim e leftLim
				//assim a nova posição do centro do cursor será:
				int posC = leftLim + (int)((rightLim - leftLim) * xn);
				//int posC = Convert.ToInt32(xn);
                //Garantir que sempre estará dentro dos limites
                if (posC < leftLim) posC = leftLim;
                if (posC > rightLim) posC = rightLim;

                //posicionar o cursor
                pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Left = posC - pictureBox_Cursor.Width / 2));
                theSession.PosPixels[trialCounter,targetCounter].Enqueue(pictureBox_Cursor.Left);
				theSession.triggerLocation[trialCounter, targetCounter].Enqueue(0);
				//Configura o novo valor máximo do cursor
				//Este valor será utilizado para calcular o erro em relação ao target

				if (xn > maxCursorPosition)
                    maxCursorPosition = xn;
                //Se o cursor retornar mais que 5% do seu máximo enquanto estiver se movendo ao target
                //então ele oscilou demais e isso é considerado um undershoot
                else if ((maxCursorPosition - xn) > (((double)theSession.SVIPT_TaskOscilationPercent) / 100.0) && xn > 0 && cursorState == cursorMovingTarget)
                {
                    //Salva a posição no undershoot
                    cursorPosUndershoot = xn;
                    //Configura o status do cursor como undershoot
                    cursorState = cursorUndershoot;
					//seta um valor para determinar o momento em que o sinal de força atinge o alvo
					if (!flagToHit)
					{
						flagToHit = true;
						if (theSession.triggerForceSignal[0].Count > 0)
						{
							theSession.triggerForceSignal[0].Dequeue();
						}
						theSession.triggerForceSignal[0].Enqueue(2);
					}
					//trigger to location
					if (theSession.triggerLocation[trialCounter, targetCounter].Count > 0)
					{
						theSession.triggerLocation[trialCounter, targetCounter].Dequeue();
					}
					theSession.triggerLocation[trialCounter, targetCounter].Enqueue(1);
					//Atualiza o status para undershoot
					label_Status.Invoke(new Action(() => label_Status.Text = ""));
				}

                //Atualiza o status do cursor
                //Quando o cursor passa por home, ele está movendo em direção a algum target
                //Se o cursor estava indo para algum target e chega em "home", então um target
                //foi atingido
                if (pictureBox_Cursor.Left >= (cursorHome + pictureBox_Cursor.Width) && cursorState == cursorAtHome)
                {
                    //Atualiza o status do cursor para indicar que ele está se movimentando
                    this.cursorState = cursorMovingTarget;
					//marca o momento em que a pessoa está em home
					if (flagToHome)
					{
						flagToHome = false;
						if (theSession.triggerForceSignalStart[0].Count > 0)
						{
							theSession.triggerForceSignalStart[0].Dequeue();
						}
						theSession.triggerForceSignalStart[0].Enqueue(1);
					}
					//trigger to location
					if (theSession.triggerLocation[trialCounter, targetCounter].Count > 0)
					{
						theSession.triggerLocation[trialCounter, targetCounter].Dequeue();
					}
					theSession.triggerLocation[trialCounter, targetCounter].Enqueue(-1);
					label_GoStop.Invoke(new Action(() => label_GoStop.Text = "GO"));
					if (flagToInvisible)
					{
						pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Visible = false));
					}
					else 
					{
						pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Visible = true));
					}

				}
                //Se o cursor está movendo em direção à um alvo e ultrapassou o seu limite esquerdo por pelo menos 50%
                //Então o alvo foi atingido
                else if(pictureBox_Cursor.Left + pictureBox_Cursor.Width/2 >= leftLimits && cursorState == cursorMovingTarget)
                {
                    cursorState = cursorHitTarget;
					//seta um valor para determinar o momento em que o sinal de força atinge o alvo
					if (!flagToHit)
					{
						flagToHit = true;
						if (theSession.triggerForceSignal[0].Count > 0)
						{
							theSession.triggerForceSignal[0].Dequeue();
						}
						theSession.triggerForceSignal[0].Enqueue(1);
						flagToHit = true;
					}
					//trigger to location
					if (theSession.triggerLocation[trialCounter, targetCounter].Count > 0)
					{
						theSession.triggerLocation[trialCounter, targetCounter].Dequeue();
					}
					theSession.triggerLocation[trialCounter, targetCounter].Enqueue(1);
					label_Status.Invoke(new Action(() => label_Status.Text = "Acertou!"));
					label_Status.Invoke(new Action(() => label_Status.Text = ""));
					label_GoStop.Invoke(new Action(() => label_GoStop.Text = "GO"));
					//pausar a thread aquisição e colocar o cursor no começo
					//pictureBox_GoStop.BackColor = Color.DarkRed;
					//label_GoStop.Invoke(new Action(() => label_GoStop.Text = "STOP"));
					//threadAcquisitionHandler.sleep(1000);
					//pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Left = cursorHome));
					//label_GoStop.Invoke(new Action(() => label_GoStop.Text = "GO"));
					//pictureBox_GoStop.BackColor = Color.LimeGreen;


				}
				
                //Se o cursor ultrapassar o alvo desejado em até 50% do seu tamanho
                //Então aconteceu um overshoot
                else if(pictureBox_Cursor.Right - pictureBox_Cursor.Width/2 >= rightLimits && cursorState == cursorHitTarget)
                {
                    cursorState = cursorOvershoot;
					//seta um valor quando é  overshoot
					if (!flagToHit)
					{
						flagToHit = true;
						if (theSession.triggerForceSignal[0].Count > 0)
						{
							theSession.triggerForceSignal[0].Dequeue();
						}
						theSession.triggerForceSignal[0].Enqueue(3);
					}
					//trigger to location
					if (theSession.triggerLocation[trialCounter, targetCounter].Count > 0)
					{
						theSession.triggerLocation[trialCounter, targetCounter].Dequeue();
					}
					theSession.triggerLocation[trialCounter, targetCounter].Enqueue(1);
					label_Status.Invoke(new Action(() => label_Status.Text = ""));
				}
                else if (pictureBox_Cursor.Left <= 1.5*cursorHome && cursorState != cursorAtHome)
				{
					if (flagToInvisible)
					{
						pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Visible = true));
					}
					//Calcula o erro do cursor em relação ao target                    
					theSession.error[trialCounter, targetCounter] = maxCursorPosition;
                    //Zera a variável que armazena o máximo deslocamento do cursor
                    maxCursorPosition = 0;
					label_GoStop.Invoke(new Action(() => label_GoStop.Text = "GO"));
					

					//Avalia o resultado do movimento
					//Undershoot, overshoot?
					//Se nenhum dos dois tiver ocorrido, então foi um acerto
					//Se o cursor voltou à home sem atingir o target, então foi undershoot
					//Se o cursor oscilou demais, então foi undershoot
					if (cursorState == cursorMovingTarget || cursorState == cursorUndershoot)
					{
						theSession.undershoot[trialCounter, targetCounter] = true;
						
						//Calcula o erro do cursor em relação ao target
						//Se ocorreu um undershoot por oscilação, deve-se levar em consideração
						//a posição dada por "cursorPosUndershoot"           
						if (cursorPosUndershoot > 0)
							theSession.error[trialCounter, targetCounter] = cursorPosUndershoot;
						//Zera a posição do undershoot
						cursorPosUndershoot = 0;
					}
					//Se o cursor ultrapassou o target, então foi overshoot
					else if (cursorState == cursorOvershoot)
					{
						theSession.overshoot[trialCounter, targetCounter] = true;
						
					}
                    
                    //Status do cursor: Está em Home
                    this.cursorState = cursorAtHome;

					targetCounter++;
					contadorIda++;
					contadorToStop++;
					

					//Zera o contador de target, caso ultrapasse o máximo de targets
					if (targetCounter >= theSession.numberOfTrials)
                    {
						flagToHit = false;
						flagToHome = true;
						trialCounter++; //Incrementa o contador de repetições (trials)
						targetCounter = 0;
                        pictureBox_GoStop.BackColor = Color.LimeGreen;
						label_GoStop.Invoke(new Action(() => label_GoStop.Text = "GO"));
						
						if(contadorToStop>=5)
						{
							//pictureBox_Cursor.Invoke(new Action(()=> pictureBox_Cursor.Visible = false));
							flagToInvisible = true;
							if (contadorToStop >= 10)
							{
								theSession.bloco++;
								stopForAWhile();
								label_GoStop.Invoke(new Action(() => label_GoStop.Text = "Aguardar"));
								//pictureBox_Cursor.Invoke(new Action(() => pictureBox_Cursor.Visible = true));
								flagToInvisible = false;
								wait(1);
								returnAfterWhile();
								contadorToStop = 0;
								waitJustALittle(2);
								label_GoStop.Invoke(new Action(() => label_GoStop.Text = "Recomeçar"));
							}
						}

						//if (theSession.bloco >= 6 && theSession.bloco < 18)
						//{
						//	flagToChangeCurve = true;
						//}
						//else if (theSession.bloco < 6 || theSession.bloco >= 18)
						//{
						//	flagToChangeCurve = false;
						//}
						//if (theSession.bloco == 0)
						//{
						//	flagToChangeCurve = true;
						//}
						//Caso o total de repetições tenha sido realizada, a sessão está concluída
						//Assim, a aquisição pode ser encerrada
						if (trialCounter >= theSession.SVIPT_TaskRepetitions)
						{
							//Encerra a aquisição
							this.StopAcquisition();
							//A sessão terminou, atualizando o label_Status
							label_Status.Invoke(new Action(() => label_Status.Text = "Sessão encerrada!"));
							//Atualiza o status do cursor para parado
							this.cursorState = cursorAtHome;
							//Muda o label para STOP
							label_GoStop.Invoke(new Action(() => label_GoStop.Text = "STOP"));
							//Salvar o arquivo de dados?
							theSession.saveDataFile(pathToSave);
						}
						else //Um repetição foi completa, portanto, escreve no label que o próximo alvo é o 1
							label_Status.Invoke(new Action(() => label_Status.Text = ""));
					}
                    else
						//Escreve qual é o próximo alvo no label
						label_Status.Invoke(new Action(() => label_Status.Text = ""));
				}
                else if (pictureBox_Cursor.Left == cursorHome && cursorState == cursorAtHome)
					//Escreve qual é o próximo alvo no label
					label_Status.Invoke(new Action(() => label_Status.Text = ""));
			}
            else
                this.threadAcquisitionHandler.mutex.ReleaseMutex(); //Libera o mutex
        }

		//funçao para contar tempo
		private void wait(int timeMinutes)
		{
			DateTime timeInicial = DateTime.Now;
			while(DateTime.Now.Subtract(timeInicial).TotalMinutes<timeMinutes)
			{
				int i = 3;
			}
		}
		//funçao para contar tempo
		private void waitJustALittle(int timeSeconds)
		{
			DateTime timeInicial = DateTime.Now;
			while (DateTime.Now.Subtract(timeInicial).TotalSeconds < timeSeconds)
			{
				int i = 3;
			}
		}
		//Função de aquisição de dados
		private void Aquisicao()
        {
            if (tDataObj.serialDataBC.count() > 0 && indicePackHeader < TeensyComCommands.tCMDSize)
            {
                packageHeader[indicePackHeader] = (char)tDataObj.serialDataBC.read();
                indicePackHeader++;
                //chegou header completo?
                string s = new string(packageHeader);
                if (string.Compare(TeensyComCommands.tCMD_StartAquis, s) == 0 && indicePackHeader == TeensyComCommands.tCMDSize)
                {//checa se a resposta do teensy eh pro pedido de startAquis, 
                    fim = false;
                    i = 0;
                    while (fim == false)//repete ate ter lido todos os bytes da mensagem
                    {
                        if (tDataObj.serialDataBC.count() > 0 && i == 0 && gotPackSize == false)
                        { //apenas na primeira iteracao le o primeiro byte depois do "STAQ", que eh o byte que diz o tamanho do pacote enviado
                            oneNumbAsByte[i] = (byte)tDataObj.serialDataBC.read();
                            PackSize = (int)oneNumbAsByte[i];
                            gotPackSize = true;
                        }
                        if (tDataObj.serialDataBC.count() > 1 && i < PackSize && gotPackSize == true)
                        {//ira ler ate que i alcance o tamanho do pacote de dados inteiro
                            oneNumbAsByte[0] = (byte)tDataObj.serialDataBC.read();
                            i++;
                            oneNumbAsByte[1] = (byte)tDataObj.serialDataBC.read();
                            i++;
                            short num = (short)((oneNumbAsByte[0] << 8) | oneNumbAsByte[1]);

                            //#######################################################################
                            //Autor: Andrei
                            //Data: 21/03/2017
                            //Modificação: Comentada a linha com o "num++"
                            //Adicionei uma condição na função de normalização
                            //para que ela retorne 0, em caso do valor passado
                            //ser menor ou igual a zero, portanto não há necessidade
                            //deste incremento
                            //num++;//para evitar ter log negativo
                            //#######################################################################

                            //Retira uma amostra da janela usada para media movel
                            theSession.JanelaMM.Dequeue();
                            //Insere a nova amostra na janela usada para a media movel
                            theSession.JanelaMM.Enqueue(num);

                            //Calcula a soma das amostras na janela de processamento
                            double soma = 0;
                            for (int k = 0; k < theSession.TamanhoJanela; k++)
                            {
                                soma += theSession.JanelaMM.ElementAt(k);
                            }
                            //A nova amostra é dada pela média das amostras da janela de processamento
                            currentPackage[(i / 2) - 1] = ((soma) / theSession.TamanhoJanela);
                        }

                        if (i >= PackSize && gotPackSize == true)
                        {//Se acabou de pegar todos os dados desse pacote
                            threadAcquisitionHandler.mutex.WaitOne();
                            for (i = 0; i < (PackSize / 2); i++)
                            {//passa todos os dados para a lista
                                dataQueue.Enqueue(currentPackage[i]);
                                theSession.data.Add(currentPackage[i]);
								theSession.triggerForceSignal[0].Enqueue(0);
								theSession.triggerForceSignalStart[0].Enqueue(0);
                                theSession.ListWriteIndex++;
                                countNewSamples++;
                            }
                            //reinicia as variaveis para coletar outro pacote de dados
                            fim = true;
                            indicePackHeader = 0;
                            gotPackSize = false;
                            for (i = 0; i < TeensyComCommands.tCMDSize; i++)
                            {
                                packageHeader[i] = '\0'; //reinicializa o array
                                s = new string(packageHeader);
                            }
                            threadAcquisitionHandler.mutex.ReleaseMutex();
                        }
                    }
                }
                else
                {
                    //Ainda não chegou header.
                    //se packHeader já estiver cheio, vamos deslizar os chars para esquerda
                    //e liberar a última posição para continuar tentando completar o header.
                    if (indicePackHeader == TeensyComCommands.tCMDSize)
                    {
                        for (i = 1; i < TeensyComCommands.tCMDSize; i++)
                            packageHeader[i - 1] = packageHeader[i];
                        packageHeader[i - 1] = '\0'; //zera packHeader[ultimaPosicao]
                        indicePackHeader--; //liberar uma posição (ultima) para próximo char do header
                    }
                }
            }
        }

        private void panel_fdbkArea_Paint(object sender, PaintEventArgs e)
        {

        }

		private void pastaToolStripMenuItem_Click(object sender, EventArgs e)
		{
			FolderBrowserDialog openFileDialog = new FolderBrowserDialog();
			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				pathToSave = openFileDialog.SelectedPath;
			}

		}

		private void MenuTS_AnalizarSessao_Click(object sender, EventArgs e)
        {
            Form AnalisarF = new Analisar();
            AnalisarF.ShowDialog();
        }

        public void continua(bool resultado)
        {
            if (resultado)
            {
                handshake();
                if (tDataObj.ad_TxAmost == -1 || tDataObj.firmwareVersion == "" || tDataObj.ad_nBits == -1 || tDataObj.ad_Range == -1)
                {
                    label_Status.Text = "Ocorreu um erro na comunicacao com o Teensy\nPor favor tente conectar novamente.";
                }
                else
                {
                    label_Status.Text = "Handshake completo\nPronto para iniciar coleta de sinal.";
                    enableAllControls();
                    MenuConectar.Enabled = false;
                }
            }
            else
                label_Status.Text = "Não consigo abrir canal de comunicação Host<->Teensy via USB.\nPor favor tente denovo.";
        }
    }
}
