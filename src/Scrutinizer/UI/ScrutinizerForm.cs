﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pyramid.Scrutinizer.UI
{
    public partial class ScrutinizerForm : Form
    {
        private List<IInstruction> m_Ops;
        private List<BasicBlock> m_Blocks;
        private List<Loop> m_Loops;
        private IScrutinizer m_Backend;

        private HashSet<InstructionWidget> m_SelectedOps = new HashSet<InstructionWidget>();
        private InstructionWidget m_LastClicked = null;

        private Dictionary<IInstruction, InstructionWidget> m_InstructionWidgets = new Dictionary<IInstruction, InstructionWidget>();
        private Dictionary<IInstruction, int> m_InstructionIndexMap = new Dictionary<IInstruction, int>();

        private void OnInstructionClick( InstructionWidget w, MouseEventArgs e )
        {
            if (e.Button == MouseButtons.Left)
            {
                if (Form.ModifierKeys == Keys.Control)
                {
                    // ctrl+click: toggle state of one widget
                    w.Selected = !w.Selected;
                    w.Refresh();
                    if (w.Selected)
                        m_SelectedOps.Add(w);
                    else
                        m_SelectedOps.Remove(w);
                }
                else if( Form.ModifierKeys == Keys.Shift && m_LastClicked != null )
                {
                    // shift+click: select everything between last clicked widget
                    //  and current widget
                   

                    int i = m_InstructionIndexMap[m_LastClicked.Instruction];
                    int j = m_InstructionIndexMap[w.Instruction];
                    int first = Math.Min(i, j);
                    int last = Math.Max(i, j);
                    for( int x=first; x<=last; x++ )
                    {
                        InstructionWidget widget = m_InstructionWidgets[m_Ops[x]];
                        m_SelectedOps.Add(widget);
                        widget.Selected = true;
                        widget.Refresh();
                    }
                }
                else
                {
                    // unmodified click.  Select only the clicked widget
                    foreach (InstructionWidget i in m_SelectedOps)
                    {
                        i.Selected = false;
                        i.Refresh();
                    }
                    m_SelectedOps.Clear();

                    w.Selected = true;
                    w.Refresh();
                    m_SelectedOps.Add(w);
                }
                
                m_LastClicked = w;

            }
            else if (e.Button == MouseButtons.Right)
            {
                // de-select everything on a right click
                foreach (InstructionWidget i in m_SelectedOps)
                {
                    i.Selected = false;
                    i.Refresh();
                }
            }
        }


        public ScrutinizerForm( IAMDShader sh )
        {
            InitializeComponent();

            try
            {
                Wrapper w = new Wrapper();
                m_Backend = sh.CreateScrutinizer();

                List<IInstruction> Ops = m_Backend.BuildProgram();

                m_Ops = Ops;
                m_Blocks = Algorithms.BuildBasicBlocks(Ops);
                if (!Algorithms.IsCFGReducible(m_Blocks))
                {
                    MessageBox.Show("Non-reducible flow-graph detected.  Can't analyze this.");
                    return;
                }

                Algorithms.FindDominators(m_Blocks);

                m_Loops = Algorithms.FindLoops(m_Blocks);
                Algorithms.ClassifyBranches(m_Ops);

                int Y = 0;
                foreach (BasicBlock b in m_Blocks)
                {
                    foreach (IInstruction op in b.Instructions)
                    {
                        InstructionWidget widget = new InstructionWidget(op);
                        m_InstructionIndexMap.Add(op, m_InstructionWidgets.Count);
                        m_InstructionWidgets.Add(op, widget);
                        
                        widget.Click += delegate(object s, EventArgs e)
                        {
                            this.OnInstructionClick(widget, e as MouseEventArgs);
                        };

                        // change format on all selected instructions whenever one of them is changed
                        widget.TexelFormatChanged += delegate(ITextureInstruction inst )
                        {
                            if (!widget.Selected)
                                return;
                            foreach( InstructionWidget s in m_SelectedOps )
                            {
                                ITextureInstruction tx = s.Instruction as ITextureInstruction;
                                if( tx != null && tx != inst )
                                    tx.Format = inst.Format;
                                s.RefreshInstruction();
                            }
                        };

                        // change filter on all selected instructions whenever one of them is changed
                        widget.FilterChanged += delegate(ISamplingInstruction inst)
                        {
                            if (!widget.Selected)
                                return;

                            foreach (InstructionWidget s in m_SelectedOps)
                            {
                                ISamplingInstruction tx = s.Instruction as ISamplingInstruction;
                                if (tx != null && tx != inst)
                                    tx.Filter = inst.Filter;
                                s.RefreshInstruction();
                            }
                        };

                        panel1.Controls.Add(widget);
                        widget.Top = Y;
                        widget.Left = 0;
                        Y += widget.Height;
                    }
                    Y += 15;
                }

                cfgWidget1.SetProgram(m_Loops, m_Blocks);
            }
            catch( System.Exception ex )
            {
                MessageBox.Show(ex.Message);
            }


        }

        private void cfgWidget1_BlockSelected(object sender, BasicBlock SelectedBlock)
        {
            foreach (InstructionWidget w in m_InstructionWidgets.Values)
                w.Brush = Brushes.DarkGray ;

            foreach (IInstruction op in SelectedBlock.Instructions)
                m_InstructionWidgets[op].Brush = Brushes.Black;

            panel1.Refresh();
            panel1.Focus();

            txtLoopCount.Visible = false;
            lblIterations.Visible = false;
        }

        private void cfgWidget1_LoopSelected(object sender, Loop SelectedLoop)
        {
            foreach (InstructionWidget w in m_InstructionWidgets.Values)
                w.Brush = Brushes.DarkGray;

            foreach( BasicBlock b in SelectedLoop.Blocks )
                foreach (IInstruction op in b.Instructions)
                    m_InstructionWidgets[op].Brush = Brushes.Black;

            panel1.Refresh();
            panel1.Focus();

            lblIterations.Visible = true;
            txtLoopCount.Visible = true;
            txtLoopCount.Text = SelectedLoop.DesiredIterations.ToString();
        }

        private void cfgWidget1_SelectionCleared(object sender)
        {
            foreach (InstructionWidget w in m_InstructionWidgets.Values)
                w.Brush = Brushes.Black;

            panel1.Refresh(); 
            panel1.Focus();

            txtLoopCount.Visible = false;
            lblIterations.Visible = false;
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            panel1.Focus();
        }

        private void txtLoopCount_TextChanged(object sender, EventArgs e)
        {
            int n = 0;
            try
            {
                n = Convert.ToInt32(txtLoopCount.Text);
                cfgWidget1.SelectedLoop.DesiredIterations = n;
            }
            catch(System.Exception )
            {
            }

        }

        private void btnSimulate_Click(object sender, EventArgs e)
        {
            List<IInstruction> trace = Algorithms.DoTrace(m_Ops, m_Blocks, m_Loops);

            string sim = m_Backend.AnalyzeExecutionTrace(trace);
            MessageBox.Show(sim);
            panel1.Refresh();
        }


    }
}
